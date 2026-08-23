using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// 22:00 UTC MON-FRI (19:00 BRT, grupo MarketDataIngest): fluxo diário pós-fechamento
/// respeitando o orçamento Brapi — UMA chamada batch para o universo inteiro, fila de
/// proventos espaçada ≥ 7 s, gaps por ticker caem para o Yahoo sidecar e depois para
/// o TradingView sidecar (cadeia Brapi → Yahoo → TV).
/// </summary>
[DisallowConcurrentExecution]
public sealed class MarketDataDailySyncJob : IJob
{
    private readonly IDailyCloseSyncService _dailyCloseService;
    private readonly ILogger<MarketDataDailySyncJob> _logger;

    public MarketDataDailySyncJob(
        IDailyCloseSyncService dailyCloseService,
        ILogger<MarketDataDailySyncJob> logger
    )
    {
        _dailyCloseService = dailyCloseService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz:MarketDataSync] Starting scheduled daily close sync (Brapi batch + sidecar gap fill)..."
        );
        var result = await _dailyCloseService.SyncDailyCloseAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var summary = result.Value;
            _logger.LogInformation(
                "[Quartz:MarketDataSync] Completed in {ElapsedMs}ms for {Universe} tickers ({TradeDate}, Status: {Status}).",
                summary.ElapsedMilliseconds,
                summary.UniverseSize,
                summary.TradeDate,
                summary.Status
            );
            foreach (var stage in summary.Stages)
            {
                _logger.LogInformation(
                    "[Quartz:MarketDataSync] Stage {Stage} ({Provider}) -> {Status}: {Upserts} quotes upserted{Error}.",
                    stage.Stage,
                    stage.Provider,
                    stage.Status,
                    stage.QuotesUpserted,
                    stage.Error is null ? string.Empty : $" ({stage.Error})"
                );
            }
        }
        else
        {
            _logger.LogError("[Quartz:MarketDataSync] Failed: {Error}", result.Error.Message);
        }
    }
}
