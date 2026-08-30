using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Budget-aware daily close pipeline. Brapi contributes exactly one batch request per
/// trading day; everything it leaves uncovered goes to the sidecars in chain order
/// (Yahoo → TradingView). Every stage writes its own <c>sync_job_logs</c> row and a
/// failing stage never aborts the others.
/// </summary>
public class DailyCloseSyncService : IDailyCloseSyncService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly BrapiClient _brapiClient;
    private readonly YfinanceSidecarClient _yahooClient;
    private readonly TradingViewSidecarClient _tradingViewClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DailyCloseSyncService> _logger;

    public DailyCloseSyncService(
        IndexDeskDbContext dbContext,
        BrapiClient brapiClient,
        YfinanceSidecarClient yahooClient,
        TradingViewSidecarClient tradingViewClient,
        IConfiguration configuration,
        ILogger<DailyCloseSyncService> logger
    )
    {
        _dbContext = dbContext;
        _brapiClient = brapiClient;
        _yahooClient = yahooClient;
        _tradingViewClient = tradingViewClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<DailyCloseSummary>> SyncDailyCloseAsync(
        IReadOnlyList<string>? targetTickers = null,
        int lookbackDays = 5,
        DateOnly? targetDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var tradeDate = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = tradeDate.AddDays(-Math.Max(1, lookbackDays));
        // Brapi free exposes today's snapshot only; for catch-up of past dates we
        // skip the batch stage entirely and let the sidecar chain do the work.
        var brapiBatchEligible = tradeDate == today;

        var tickers = targetTickers is { Count: > 0 }
            ? targetTickers
            : SyncUniverse.FromList(_configuration[SyncUniverse.DailyTickersConfigKey]);
        if (tickers.Count == 0)
        {
            tickers = await SyncUniverse.ResolveAsync(
                _dbContext,
                null,
                cancellationToken: cancellationToken
            );
        }

        _logger.LogInformation(
            "[DailyClose] Starting for {Count} tickers ({WindowStart} to {TradeDate})...",
            tickers.Count,
            windowStart,
            tradeDate
        );

        // Resolve assets once; unknown tickers are skipped (never fabricated).
        var assetsByTicker = await _dbContext
            .Assets.Where(a => tickers.Contains(a.Ticker))
            .ToDictionaryAsync(a => a.Ticker, cancellationToken);

        var stages = new List<StageSummary>();

        // ---- Stage 1: the single allowed Brapi batch call -------------------------
        var (batchStage, coveredTickers) = await RunBrapiBatchStageAsync(
            brapiBatchEligible,
            tickers,
            tradeDate,
            assetsByTicker,
            cancellationToken
        );
        stages.Add(batchStage);

        // ---- Stage 2: Brapi dividend queue (spaced ≥ 7 s, budget rule) ------------
        var divSw = Stopwatch.StartNew();
        var divStartedAt = DateTimeOffset.UtcNow;
        var divUpserts = 0;
        var divTouched = 0;
        string? divError = null;
        var dividendSpacingMs = DailyCloseChain.ResolveDividendSpacingMs(_configuration);

        // Spacing/classification live in DividendQueue (pure, unit-tested); this lambda
        // carries the DB-bound work: fetch via the pool-backed client + idempotent upsert.
        // Dividends are only served for "today" runs too (same budget rule as batch).
        var eligibleForBrapi = brapiBatchEligible
            ? tickers.Where(_brapiClient.SupportsTicker).ToList()
            : [];
        var divOutcome = brapiBatchEligible
            ? await DividendQueue.RunAsync(
                eligibleForBrapi.Where(t => assetsByTicker.ContainsKey(t)).ToList(),
                async (ticker, token) =>
                {
                    var dividends = await _brapiClient.GetDividendsAsync(ticker, token);
                    if (dividends.IsFailure)
                    {
                        return Result<int>.Failure(dividends.Error);
                    }

                    if (!assetsByTicker.TryGetValue(ticker, out var asset))
                    {
                        return Result<int>.Success(0);
                    }

                    divTouched++;
                    var count = await IngestionUpserts.DividendsAsync(
                        _dbContext,
                        asset.Id,
                        dividends.Value,
                        token
                    );
                    divUpserts += count;
                    return Result<int>.Success(count);
                },
                ms => Task.Delay(ms, cancellationToken),
                dividendSpacingMs,
                cancellationToken
            )
            : DividendQueueOutcome.Empty;
        divError = divOutcome.AnyFailure ? divOutcome.FirstErrorMessage : null;

        var divExpected = brapiBatchEligible
            ? eligibleForBrapi.Count(t => assetsByTicker.ContainsKey(t))
            : 0;
        var dividendStatus =
            divExpected == 0
                ? DailyCloseChain.Success
                : DailyCloseChain.StatusFor(
                    divOutcome.FirstErrorCode,
                    divOutcome.AnyHardFailure,
                    divTouched,
                    divExpected
                );

        stages.Add(
            new StageSummary(
                "BrapiDividends",
                "BRAPI",
                dividendStatus,
                divUpserts,
                divTouched,
                divError
            )
        );
        await LogStageAsync(
            "DailyClose_BrapiDividends",
            "BRAPI",
            dividendStatus,
            processed: divExpected,
            updated: divUpserts,
            error: divError,
            timeMs: (int)divSw.ElapsedMilliseconds,
            startedAt: divStartedAt,
            cancellationToken: cancellationToken
        );

        // ---- Stage 3/4: per-ticker gap fill via the sidecar chain ------------------
        var gaps = tickers.Where(t => !coveredTickers.Contains(t)).ToList();

        // A ticker counts as covered when it already has a bar inside the lookback
        // window (holidays/weekends must not trigger pointless sidecar spawns).
        var recentDates = await _dbContext
            .AssetQuotes.Where(q =>
                tickers.Contains(q.Asset!.Ticker) && q.Date >= windowStart && q.Date <= tradeDate
            )
            .Select(q => q.Asset!.Ticker)
            .Distinct()
            .ToListAsync(cancellationToken);
        gaps = gaps.Except(recentDates, StringComparer.OrdinalIgnoreCase).ToList();

        var yahooSw = Stopwatch.StartNew();
        var yahooStartedAt = DateTimeOffset.UtcNow;
        var yahooUpserts = 0;
        var yahooTouched = 0;
        string? yahooError = null;
        string? yahooErrorCode = null;
        var yahooHardFailure = false;
        var stillMissing = new List<string>();

        foreach (var ticker in gaps)
        {
            var (upserted, errorCode, errorMessage) = await FetchAndStoreAsync(
                _yahooClient,
                ticker,
                assetsByTicker.GetValueOrDefault(ticker)?.Id,
                windowStart,
                tradeDate,
                cancellationToken
            );
            if (upserted > 0)
            {
                yahooUpserts += upserted;
                yahooTouched++;
            }
            else
            {
                if (errorCode is not null)
                {
                    yahooError ??=
                        $"YahooSidecar could not cover {ticker} [{errorCode}]"
                        + (errorMessage is null ? string.Empty : $": {errorMessage}");
                    yahooErrorCode ??= errorCode;
                    if (!DailyCloseChain.SoftFailureCodes.Contains(errorCode))
                    {
                        yahooHardFailure = true;
                    }
                }
                else
                {
                    // No asset curated or ticker unsupported — expected skip, not an error.
                }

                stillMissing.Add(ticker);
            }

            await Task.Delay(200, cancellationToken);
        }

        var yahooStatus = DailyCloseChain.StatusFor(
            yahooErrorCode,
            yahooHardFailure,
            yahooTouched,
            gaps.Count
        );

        stages.Add(
            new StageSummary(
                "YahooGapFill",
                "YAHOOSIDECAR",
                yahooStatus,
                yahooUpserts,
                yahooTouched,
                yahooError
            )
        );
        await LogStageAsync(
            "DailyClose_YahooGapFill",
            "YAHOOSIDECAR",
            yahooStatus,
            processed: gaps.Count,
            updated: yahooUpserts,
            error: yahooError,
            timeMs: (int)yahooSw.ElapsedMilliseconds,
            startedAt: yahooStartedAt,
            cancellationToken: cancellationToken
        );

        var tvSw = Stopwatch.StartNew();
        var tvStartedAt = DateTimeOffset.UtcNow;
        var tvUpserts = 0;
        var tvTouched = 0;
        string? tvError = null;
        string? tvErrorCode = null;
        var tvHardFailure = false;

        foreach (var ticker in stillMissing)
        {
            var (upserted, errorCode, errorMessage) = await FetchAndStoreAsync(
                _tradingViewClient,
                ticker,
                assetsByTicker.GetValueOrDefault(ticker)?.Id,
                windowStart,
                tradeDate,
                cancellationToken
            );
            if (upserted > 0)
            {
                tvUpserts += upserted;
                tvTouched++;
            }
            else if (errorCode is not null)
            {
                tvError ??=
                    $"TradingView could not cover {ticker} [{errorCode}]"
                    + (errorMessage is null ? string.Empty : $": {errorMessage}");
                tvErrorCode ??= errorCode;
                if (!DailyCloseChain.SoftFailureCodes.Contains(errorCode))
                {
                    tvHardFailure = true;
                }
            }

            await Task.Delay(200, cancellationToken);
        }

        var tvExpected = stillMissing.Count;
        var tvStatus = DailyCloseChain.StatusFor(tvErrorCode, tvHardFailure, tvTouched, tvExpected);

        stages.Add(
            new StageSummary(
                "TradingViewGapFill",
                "TRADINGVIEW",
                tvStatus,
                tvUpserts,
                tvTouched,
                tvError
            )
        );
        await LogStageAsync(
            "DailyClose_TradingViewGapFill",
            "TRADINGVIEW",
            tvStatus,
            processed: tvExpected,
            updated: tvUpserts,
            error: tvError,
            timeMs: (int)tvSw.ElapsedMilliseconds,
            startedAt: tvStartedAt,
            cancellationToken: cancellationToken
        );

        sw.Stop();
        var overall = DailyCloseChain.Overall(stages.Select(s => s.Status).ToList());

        return Result<DailyCloseSummary>.Success(
            new DailyCloseSummary(
                Status: overall,
                TradeDate: tradeDate,
                UniverseSize: tickers.Count,
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                Stages: stages
            )
        );
    }

    /// <summary>Fetch from one client and upsert when the asset is curated. Returns the
    /// number of quotes persisted (0 = nothing usable) plus the failure code/message when
    /// the provider itself reported an error (soft codes let the stage degrade to
    /// PARTIAL_WARNING instead of FAILED).</summary>
    private async Task<(int Upserted, string? ErrorCode, string? ErrorMessage)> FetchAndStoreAsync(
        IMarketDataClient client,
        string ticker,
        Guid? assetId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken
    )
    {
        if (assetId is null || !client.SupportsTicker(ticker))
        {
            return (0, null, null);
        }

        var fetch = await client.GetHistoricalQuotesAsync(
            ticker,
            startDate,
            endDate,
            cancellationToken
        );
        if (fetch.IsFailure)
        {
            return (0, fetch.Error.Code, fetch.Error.Message);
        }

        if (fetch.Value.Count == 0)
        {
            return (0, "Provider.NoCoverage", $"{client.ProviderName} returned no bars");
        }

        try
        {
            var upserted = await IngestionUpserts.QuotesAsync(
                _dbContext,
                assetId.Value,
                fetch.Value,
                cancellationToken
            );
            return (upserted, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DailyClose] Upsert failed for {Ticker} from {Provider}.",
                ticker,
                client.ProviderName
            );
            return (0, "DailyClose.UpsertFailed", ex.Message);
        }
    }

    private async Task<int> UpsertPerAssetAsync(
        IReadOnlyList<Domain.NormalizedQuote> quotes,
        IReadOnlyDictionary<string, AssetEntity> assetsByTicker,
        CancellationToken cancellationToken
    )
    {
        var total = 0;
        foreach (var group in quotes.GroupBy(q => q.Ticker))
        {
            if (!assetsByTicker.TryGetValue(group.Key, out var asset))
            {
                continue;
            }

            total += await IngestionUpserts.QuotesAsync(
                _dbContext,
                asset.Id,
                group.ToList(),
                cancellationToken
            );
        }

        return total;
    }

    private async Task LogStageAsync(
        string jobName,
        string provider,
        string status,
        int processed,
        int updated,
        string? error,
        int timeMs,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _dbContext.SyncJobLogs.Add(
                new SyncJobLogEntity
                {
                    JobName = jobName,
                    ProviderName = provider,
                    Status = status,
                    RecordsProcessed = processed,
                    RecordsUpdated = updated,
                    RecordsSkipped = 0,
                    ErrorDetails = error,
                    ExecutionTimeMs = timeMs,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                }
            );
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DailyClose] Failed to write sync_job_logs entry.");
        }
    }

    private async Task<(StageSummary Stage, HashSet<string> CoveredTickers)> RunBrapiBatchStageAsync(
        bool brapiBatchEligible,
        IReadOnlyList<string> tickers,
        DateOnly tradeDate,
        IReadOnlyDictionary<string, AssetEntity> assetsByTicker,
        CancellationToken cancellationToken
    )
    {
        var batchStartedAt = DateTimeOffset.UtcNow;
        var batchSw = Stopwatch.StartNew();
        var coveredTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!brapiBatchEligible)
        {
            await LogStageAsync(
                "DailyClose_BrapiBatch",
                "BRAPI",
                DailyCloseChain.Success,
                processed: 0,
                updated: 0,
                error: null,
                timeMs: 0,
                startedAt: batchStartedAt,
                cancellationToken: cancellationToken
            );

            return (
                new StageSummary("BrapiBatch", "BRAPI", DailyCloseChain.Success, 0, 0, null),
                coveredTickers
            );
        }

        var eligibleForBrapi = tickers.Where(_brapiClient.SupportsTicker).ToList();
        var brapiQuotes = 0;
        string? brapiError = null;
        string? brapiErrorCode = null;

        if (eligibleForBrapi.Count > 0)
        {
            var batch = await _brapiClient.GetDailyBatchQuotesAsync(
                eligibleForBrapi,
                tradeDate,
                cancellationToken
            );
            if (batch.IsSuccess)
            {
                brapiQuotes = await UpsertPerAssetAsync(
                    batch.Value,
                    assetsByTicker,
                    cancellationToken
                );
                coveredTickers.UnionWith(batch.Value.Select(q => q.Ticker));
                _logger.LogInformation(
                    "[DailyClose] Brapi batch covered {Covered}/{Requested} tickers.",
                    coveredTickers.Count,
                    eligibleForBrapi.Count
                );
            }
            else
            {
                brapiError = batch.Error.Message;
                brapiErrorCode = batch.Error.Code;
                _logger.LogWarning(
                    "[DailyClose] Brapi batch failed: [{Code}] {Error}",
                    brapiErrorCode,
                    brapiError
                );
            }
        }

        var batchStatus = DailyCloseChain.StatusFor(
            brapiErrorCode,
            anyHardFailure: false,
            touched: coveredTickers.Count,
            expected: eligibleForBrapi.Count
        );

        await LogStageAsync(
            "DailyClose_BrapiBatch",
            "BRAPI",
            batchStatus,
            processed: eligibleForBrapi.Count,
            updated: brapiQuotes,
            error: brapiError,
            timeMs: (int)batchSw.ElapsedMilliseconds,
            startedAt: batchStartedAt,
            cancellationToken: cancellationToken
        );

        return (
            new StageSummary(
                "BrapiBatch",
                "BRAPI",
                batchStatus,
                brapiQuotes,
                coveredTickers.Count,
                brapiError
            ),
            coveredTickers
        );
    }
}
