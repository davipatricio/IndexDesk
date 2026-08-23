using System.Diagnostics;
using IndexDesk.BuildingBlocks.Cache;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

public class AssetSyncService : IAssetSyncService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IAssetBackfillService _backfillService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AssetSyncService> _logger;

    public AssetSyncService(
        IndexDeskDbContext dbContext,
        IAssetBackfillService backfillService,
        ICacheService cacheService,
        ILogger<AssetSyncService> logger
    )
    {
        _dbContext = dbContext;
        _backfillService = backfillService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<SyncExecutionSummary>> SyncDailyQuotesAsync(
        IReadOnlyList<string>? targetTickers = null,
        int lookbackDays = 5,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-lookbackDays);

        var tickers =
            targetTickers != null && targetTickers.Count > 0
                ? targetTickers
                : new[] { "MXRF11", "VWRA11", "GOLD11", "WRLD11", "IBOV", "IFIX" };

        _logger.LogInformation(
            "[Sync:DailyStart] Executing incremental daily sync for {Count} assets (Lookback: {Days} days, {StartDate} to {EndDate})...",
            tickers.Count,
            lookbackDays,
            startDate,
            today
        );

        var totalQuotes = 0;
        var totalDividends = 0;
        var successfulAssets = 0;
        var failed = new List<string>();

        foreach (var ticker in tickers)
        {
            var result = await _backfillService.BackfillAssetAsync(
                ticker,
                startDate,
                today,
                cancellationToken
            );
            if (result.IsSuccess)
            {
                successfulAssets++;
                totalQuotes += result.Value.QuotesIngested;
                totalDividends += result.Value.DividendsIngested;

                // Invalidate Redis cache for this ticker
                var cacheKey = $"marketdata:quotes:{ticker.ToUpperInvariant()}";
                await _cacheService.RemoveAsync(cacheKey, cancellationToken);
            }
            else
            {
                failed.Add(ticker);
                _logger.LogWarning(
                    "[Sync:AssetFailed] Incremental sync failed for {Ticker}: {Error}",
                    ticker,
                    result.Error.Message
                );
            }

            await Task.Delay(200, cancellationToken);
        }

        sw.Stop();
        var elapsed = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "[Sync:DailyComplete] Daily sync finished in {ElapsedMs}ms ({Success}/{Total} assets succeeded, {Quotes} quotes ingested).",
            elapsed,
            successfulAssets,
            tickers.Count,
            totalQuotes
        );

        return Result<SyncExecutionSummary>.Success(
            new SyncExecutionSummary(
                Status: failed.Count == 0
                    ? "SUCCESS"
                    : (successfulAssets > 0 ? "PARTIAL_WARNING" : "FAILED"),
                AssetsSynced: successfulAssets,
                QuotesIngested: totalQuotes,
                DividendsIngested: totalDividends,
                ElapsedMilliseconds: elapsed,
                FailedTickers: failed
            )
        );
    }
}
