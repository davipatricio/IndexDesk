using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// 22:05 UTC MON-FRI (grupo MarketDataIngest): câmbio pós-fechamento via AwesomeAPI
/// (USD-BRL, EUR-BRL, BTC-BRL; bid como proxy de close). Job próprio em vez de
/// end-step do MarketDataDailySyncJob — mesmo padrão do BcbSyncJob: cada domínio de
/// dado tem shell fina + sync_job_logs + falha isolada.
/// </summary>
[DisallowConcurrentExecution]
public sealed class FxRatesDailySyncJob : IJob
{
    private readonly IFxRateSyncService _fxSyncService;
    private readonly ILogger<FxRatesDailySyncJob> _logger;

    public FxRatesDailySyncJob(
        IFxRateSyncService fxSyncService,
        ILogger<FxRatesDailySyncJob> logger
    )
    {
        _fxSyncService = fxSyncService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz:FxRates] Starting scheduled FX sync (AwesomeAPI USD/EUR/BTC-BRL)..."
        );
        var result = await _fxSyncService.SyncLatestAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var s = result.Value;
            _logger.LogInformation(
                "[Quartz:FxRates] Completed in {ElapsedMs}ms ({Persisted} records upserted, Status: {Status}).",
                s.ElapsedMilliseconds,
                s.RecordsUpserted,
                s.Status
            );
        }
        else
        {
            _logger.LogError("[Quartz:FxRates] Failed: {Error}", result.Error.Message);
        }
    }
}
