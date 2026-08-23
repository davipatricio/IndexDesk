using IndexDesk.Modules.MarketData.Ingestion.Holdings;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// Sábado 08:00 UTC (`0 0 8 ? * SAT *`, grupo MarketDataIngest): feeds oficiais das
/// gestoras (iShares CSV, SPDR XLSX, It Now/Investo HTML) upsert idempotente em
/// etf_holdings. Fonte que falha = PARTIAL_WARNING; o job segue com as demais.
/// </summary>
[DisallowConcurrentExecution]
public sealed class HoldingsWeeklySyncJob : IJob
{
    private readonly IEtfHoldingsSyncService _holdingsSyncService;
    private readonly ILogger<HoldingsWeeklySyncJob> _logger;

    public HoldingsWeeklySyncJob(
        IEtfHoldingsSyncService holdingsSyncService,
        ILogger<HoldingsWeeklySyncJob> logger
    )
    {
        _holdingsSyncService = holdingsSyncService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("[Quartz:Holdings] Starting weekly manager-feed holdings sync...");
        var result = await _holdingsSyncService.SyncWeeklyAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var s = result.Value;
            _logger.LogInformation(
                "[Quartz:Holdings] Completed in {ElapsedMs}ms ({Succeeded}/{Attempted} sources, {Rows} rows upserted, Status: {Status}).",
                s.ElapsedMilliseconds,
                s.SourcesSucceeded,
                s.SourcesAttempted,
                s.TotalRowsUpserted,
                s.Status
            );
            foreach (var source in s.Sources.Where(x => x.Status != "SUCCESS"))
            {
                _logger.LogWarning(
                    "[Quartz:Holdings] Source {Source} for {Ticker} ended {Status}: {Error}",
                    source.Source,
                    source.Ticker,
                    source.Status,
                    source.Error
                );
            }
        }
        else
        {
            _logger.LogError("[Quartz:Holdings] Failed: {Error}", result.Error.Message);
        }
    }
}
