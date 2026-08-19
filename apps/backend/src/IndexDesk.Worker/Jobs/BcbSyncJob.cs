using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed class BcbSyncJob : IJob
{
    private readonly IMacroEconomicSyncService _macroSyncService;
    private readonly ILogger<BcbSyncJob> _logger;

    public BcbSyncJob(IMacroEconomicSyncService macroSyncService, ILogger<BcbSyncJob> logger)
    {
        _macroSyncService = macroSyncService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz] Starting scheduled BCB macro indicators synchronization (CDI, Selic, IPCA)..."
        );

        // Local-first background ingestion logic
        var result = await _macroSyncService.SyncAllAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var s = result.Value;
            _logger.LogInformation(
                "[Quartz] BCB synchronization completed ({Series} series, {Records} records, Status: {Status}).",
                s.SeriesSynced,
                s.RecordsUpserted,
                s.Status
            );
        }
        else
        {
            _logger.LogError("[Quartz] BCB synchronization failed: {Error}", result.Error.Message);
        }
    }
}
