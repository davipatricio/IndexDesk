using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

public class AssetBackfillService : IAssetBackfillService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IFallbackMarketDataService _fallbackService;
    private readonly SidecarProviderDirectory _providers;
    private readonly ILogger<AssetBackfillService> _logger;

    public AssetBackfillService(
        IndexDeskDbContext dbContext,
        IFallbackMarketDataService fallbackService,
        SidecarProviderDirectory providers,
        ILogger<AssetBackfillService> logger
    )
    {
        _dbContext = dbContext;
        _fallbackService = fallbackService;
        _providers = providers;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<BackfillExecutionSummary>>> BackfillPilotAssetsAsync(
        DateOnly? defaultStartDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var pilotTickers = new[] { "MXRF11", "VWRA11", "GOLD11" };
        var summaries = new List<BackfillExecutionSummary>(pilotTickers.Length);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var ticker in pilotTickers)
        {
            // Tailored start dates per asset
            var startDate =
                defaultStartDate
                ?? ticker switch
                {
                    "MXRF11" => new DateOnly(2015, 1, 1),
                    "VWRA11" => new DateOnly(2021, 1, 1),
                    "GOLD11" => new DateOnly(2020, 1, 1),
                    _ => new DateOnly(2020, 1, 1),
                };

            var result = await BackfillAssetAsync(ticker, startDate, endDate, cancellationToken);
            if (result.IsSuccess)
            {
                summaries.Add(result.Value);
            }
            else
            {
                summaries.Add(
                    new BackfillExecutionSummary(
                        Ticker: ticker,
                        Status: "FAILED",
                        QuotesIngested: 0,
                        DividendsIngested: 0,
                        SourceProvider: "NONE",
                        ElapsedMilliseconds: 0,
                        ErrorMessage: result.Error.Message
                    )
                );
            }

            // Throttle between requests to avoid rate limits
            await Task.Delay(300, cancellationToken);
        }

        return Result<IReadOnlyList<BackfillExecutionSummary>>.Success(summaries);
    }

    public async Task<Result<BackfillExecutionSummary>> BackfillAssetAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default,
        string? preferredProvider = null
    )
    {
        ticker = ticker.ToUpperInvariant();
        var sw = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "[Backfill:Start] Starting historical backfill for {Ticker} ({StartDate} to {EndDate})...",
            ticker,
            startDate,
            endDate
        );

        try
        {
            // 1. Ensure known asset metadata exists in the database. Unknown tickers
            // cannot be persisted with fabricated metadata; curate them first.
            var assetResult = await EnsureAssetExistsAsync(ticker, cancellationToken);
            if (assetResult.IsFailure)
            {
                return Result<BackfillExecutionSummary>.Failure(assetResult.Error);
            }

            var asset = assetResult.Value;

            // 2. Fetch Quotes: explicit provider (--provider yahoo|tv|infomoney) or the
            //    Brapi → Yahoo → TV fallback chain.
            Result<IReadOnlyList<NormalizedQuote>> quotesResult;
            if (!string.IsNullOrWhiteSpace(preferredProvider))
            {
                quotesResult = await _providers.FetchQuotesFromProviderAsync(
                    preferredProvider,
                    ticker,
                    startDate,
                    endDate,
                    cancellationToken
                );
                if (quotesResult.IsFailure)
                {
                    await LogJobExecutionAsync(
                        jobName: $"Backfill_{ticker}",
                        provider: preferredProvider.ToUpperInvariant(),
                        status: "FAILED",
                        processed: 0,
                        updated: 0,
                        error: quotesResult.Error.Message,
                        timeMs: (int)sw.ElapsedMilliseconds,
                        startedAt: startedAt,
                        cancellationToken: cancellationToken
                    );

                    return Result<BackfillExecutionSummary>.Failure(quotesResult.Error);
                }
            }
            else
            {
                quotesResult = await _fallbackService.GetQuotesWithFallbackAsync(
                    ticker,
                    startDate,
                    endDate,
                    cancellationToken
                );
                if (quotesResult.IsFailure)
                {
                    await LogJobExecutionAsync(
                        jobName: $"Backfill_{ticker}",
                        provider: "ALL",
                        status: "FAILED",
                        processed: 0,
                        updated: 0,
                        error: quotesResult.Error.Message,
                        timeMs: (int)sw.ElapsedMilliseconds,
                        startedAt: startedAt,
                        cancellationToken: cancellationToken
                    );

                    return Result<BackfillExecutionSummary>.Failure(quotesResult.Error);
                }
            }

            var quotes = quotesResult.Value;
            var providerName =
                preferredProvider?.ToUpperInvariant()
                ?? quotes.FirstOrDefault().SourceProvider
                ?? "UNKNOWN";

            // 3. Upsert Quotes into PostgreSQL/TimescaleDB (idempotent — safe restart)
            var quotesCount = await IngestionUpserts.QuotesAsync(
                _dbContext,
                asset.Id,
                quotes,
                cancellationToken
            );

            // 4. Fetch and Upsert Dividends (especially relevant for MXRF11)
            var dividendsCount = 0;
            var dividendsResult = await _fallbackService.GetDividendsWithFallbackAsync(
                ticker,
                cancellationToken
            );
            if (dividendsResult.IsSuccess && dividendsResult.Value.Count > 0)
            {
                dividendsCount = await IngestionUpserts.DividendsAsync(
                    _dbContext,
                    asset.Id,
                    dividendsResult.Value,
                    cancellationToken
                );
            }

            sw.Stop();
            var timeMs = (int)sw.ElapsedMilliseconds;

            await LogJobExecutionAsync(
                jobName: $"Backfill_{ticker}",
                provider: providerName,
                status: "SUCCESS",
                processed: quotes.Count,
                updated: quotesCount,
                error: null,
                timeMs: timeMs,
                startedAt: startedAt,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation(
                "[Backfill:Completed] Finished backfill for {Ticker} in {ElapsedMs}ms ({Quotes} quotes, {Dividends} dividends, Provider: {Provider}).",
                ticker,
                timeMs,
                quotesCount,
                dividendsCount,
                providerName
            );

            return Result<BackfillExecutionSummary>.Success(
                new BackfillExecutionSummary(
                    Ticker: ticker,
                    Status: "SUCCESS",
                    QuotesIngested: quotesCount,
                    DividendsIngested: dividendsCount,
                    SourceProvider: providerName,
                    ElapsedMilliseconds: timeMs,
                    ErrorMessage: null
                )
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "[Backfill:Exception] Unhandled error backfilling {Ticker}.",
                ticker
            );

            await LogJobExecutionAsync(
                jobName: $"Backfill_{ticker}",
                provider: "UNKNOWN",
                status: "FAILED",
                processed: 0,
                updated: 0,
                error: ex.Message,
                timeMs: (int)sw.ElapsedMilliseconds,
                startedAt: startedAt,
                cancellationToken: cancellationToken
            );

            return Result<BackfillExecutionSummary>.Failure(
                Error.Failure("Backfill.Exception", ex.Message)
            );
        }
    }

    private async Task<Result<AssetEntity>> EnsureAssetExistsAsync(
        string ticker,
        CancellationToken cancellationToken
    )
    {
        await DatabaseInitializer.MigrateAsync(_dbContext, cancellationToken);

        var existing = await _dbContext.Assets.FirstOrDefaultAsync(
            a => a.Ticker == ticker,
            cancellationToken
        );
        if (existing is not null)
        {
            return Result<AssetEntity>.Success(existing);
        }

        // Benchmark indices are the one exception to "curator creates the record first":
        // their metadata is fully curated in code (BenchmarkCatalog), so ingestion can
        // create them idempotently. Regular B3 assets still require explicit curation —
        // never fabricate name, type, CNPJ or other metadata.
        if (BenchmarkCatalog.All.TryGetValue(ticker, out var benchmark))
        {
            var entity = new AssetEntity
            {
                Ticker = benchmark.Ticker,
                Name = benchmark.Name,
                AssetType = "INDEX",
                Currency = "BRL",
                TradingViewSymbol = benchmark.TradingViewSymbol,
            };
            _dbContext.Assets.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "[Backfill:Catalog] Created curated INDEX asset {Ticker} ({Name}).",
                entity.Ticker,
                entity.Name
            );
            return Result<AssetEntity>.Success(entity);
        }

        return Result<AssetEntity>.Failure(Error.NotFound("Asset.Metadata", ticker));
    }

    private async Task LogJobExecutionAsync(
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
            var log = new SyncJobLogEntity
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
            };

            _dbContext.SyncJobLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Backfill:LogFailed] Failed to write entry into sync_job_logs."
            );
        }
    }
}
