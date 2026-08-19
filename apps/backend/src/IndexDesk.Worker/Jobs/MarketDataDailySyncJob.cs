using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed class MarketDataDailySyncJob : IJob
{
    private readonly IAssetSyncService _syncService;
    private readonly ILogger<MarketDataDailySyncJob> _logger;

    public MarketDataDailySyncJob(
        IAssetSyncService syncService,
        ILogger<MarketDataDailySyncJob> logger
    )
    {
        _syncService = syncService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz:MarketDataSync] Starting scheduled incremental daily market data sync..."
        );
        var result = await _syncService.SyncDailyQuotesAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var summary = result.Value;
            _logger.LogInformation(
                "[Quartz:MarketDataSync] Completed in {ElapsedMs}ms ({Quotes} quotes, {Dividends} dividends, Status: {Status}).",
                summary.ElapsedMilliseconds,
                summary.QuotesIngested,
                summary.DividendsIngested,
                summary.Status
            );
        }
        else
        {
            _logger.LogError("[Quartz:MarketDataSync] Failed: {Error}", result.Error.Message);
        }
    }
}
