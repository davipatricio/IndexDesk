using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.Modules.MarketData.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Logic behind <c>TradingViewDailySyncJob</c>. Universe: explicit list, else the
/// configured <c>Providers:Sync:TradingViewTickers</c>, else stale detection — every
/// active asset whose most recent local quote is older than the freshness threshold.
/// </summary>
public class TradingViewRefreshSyncService : ITradingViewRefreshSyncService
{
    private const int RefreshWindowDays = 30;
    private const int FreshnessThresholdDays = 4; // covers weekends + a holiday

    private readonly IndexDeskDbContext _dbContext;
    private readonly TradingViewSidecarClient _tradingViewClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TradingViewRefreshSyncService> _logger;

    public TradingViewRefreshSyncService(
        IndexDeskDbContext dbContext,
        TradingViewSidecarClient tradingViewClient,
        IConfiguration configuration,
        ILogger<TradingViewRefreshSyncService> logger
    )
    {
        _dbContext = dbContext;
        _tradingViewClient = tradingViewClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<TvRefreshSummary>> RefreshAsync(
        IReadOnlyList<string>? targetTickers = null,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        await DatabaseInitializer.MigrateAsync(_dbContext, cancellationToken);

        var tickers = targetTickers is { Count: > 0 }
            ? targetTickers
            : SyncUniverse.FromList(_configuration[SyncUniverse.TradingViewTickersConfigKey]);
        if (tickers.Count == 0)
        {
            tickers = await StaleTickersAsync(cancellationToken);
        }

        if (tickers.Count == 0)
        {
            _logger.LogInformation("[TV:Refresh] No stale tickers; nothing to refresh.");
            return Result<TvRefreshSummary>.Success(
                new TvRefreshSummary(
                    Status: "SUCCESS",
                    TickersRequested: 0,
                    TickersRefreshed: 0,
                    QuotesUpserted: 0,
                    ElapsedMilliseconds: 0,
                    FailedTickers: Array.Empty<string>()
                )
            );
        }

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-RefreshWindowDays);

        var refreshed = 0;
        var upserted = 0;
        var failed = new List<string>();

        foreach (var ticker in tickers)
        {
            try
            {
                // Assets must be curated before ingestion touches them.
                var assetId = await _dbContext
                    .Assets.Where(a => a.Ticker == ticker)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (assetId is null)
                {
                    failed.Add(ticker);
                    _logger.LogWarning(
                        "[TV:Refresh] Skipped {Ticker}: no curated asset record.",
                        ticker
                    );
                    continue;
                }

                var fetch = await _tradingViewClient.GetHistoricalQuotesAsync(
                    ticker,
                    startDate,
                    endDate,
                    cancellationToken
                );
                if (fetch.IsFailure || fetch.Value.Count == 0)
                {
                    failed.Add(ticker);
                    continue;
                }

                upserted += await IngestionUpserts.QuotesAsync(
                    _dbContext,
                    assetId.Value,
                    fetch.Value,
                    cancellationToken
                );
                refreshed++;
            }
            catch (Exception ex)
            {
                failed.Add(ticker);
                _logger.LogError(ex, "[TV:Refresh] Error refreshing {Ticker}.", ticker);
            }

            await Task.Delay(200, cancellationToken);
        }

        sw.Stop();
        var status =
            failed.Count == 0 ? "SUCCESS"
            : refreshed > 0 ? "PARTIAL_WARNING"
            : "FAILED";

        await LogAsync(status, tickers.Count, upserted, sw, startedAt, cancellationToken);

        return Result<TvRefreshSummary>.Success(
            new TvRefreshSummary(
                Status: status,
                TickersRequested: tickers.Count,
                TickersRefreshed: refreshed,
                QuotesUpserted: upserted,
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                FailedTickers: failed
            )
        );
    }

    /// <summary>Active assets whose latest quote predates the freshness threshold.</summary>
    internal async Task<IReadOnlyList<string>> StaleTickersAsync(
        CancellationToken cancellationToken
    )
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-FreshnessThresholdDays);

        var rows = await _dbContext
            .Assets.Where(a => a.IsActive)
            .Select(a => new { a.Ticker, LastQuoteDate = a.Quotes.Max(q => (DateOnly?)q.Date) })
            .ToListAsync(cancellationToken);

        return rows.Where(r => (r.LastQuoteDate ?? DateOnly.MinValue) < threshold)
            .Select(r => r.Ticker)
            .OrderBy(t => t)
            .ToList();
    }

    private async Task LogAsync(
        string status,
        int requested,
        int upserted,
        Stopwatch sw,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _dbContext.SyncJobLogs.Add(
                new BuildingBlocks.Persistence.Entities.SyncJobLogEntity
                {
                    JobName = "TradingView_DailyRefresh",
                    ProviderName = _tradingViewClient.ProviderName.ToUpperInvariant(),
                    Status = status,
                    RecordsProcessed = requested,
                    RecordsUpdated = upserted,
                    RecordsSkipped = 0,
                    ErrorDetails = null,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                }
            );
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TV:Refresh] Failed to write sync_job_logs entry.");
        }
    }
}
