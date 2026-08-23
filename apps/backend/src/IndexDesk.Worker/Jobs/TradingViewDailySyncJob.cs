using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// 22:30 UTC MON-FRI (grupo MarketDataIngest): refresh OHLCV via sidecar TradingView
/// para tickers que a cadeia Brapi/Yahoo não cobre (benchmarks, globais) ou cuja
/// série local está defasada. Shell fina — lógica em TradingViewRefreshSyncService.
/// </summary>
[DisallowConcurrentExecution]
public sealed class TradingViewDailySyncJob : IJob
{
    private readonly ITradingViewRefreshSyncService _refreshService;
    private readonly ILogger<TradingViewDailySyncJob> _logger;

    public TradingViewDailySyncJob(
        ITradingViewRefreshSyncService refreshService,
        ILogger<TradingViewDailySyncJob> logger
    )
    {
        _refreshService = refreshService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz:TradingView] Starting scheduled TradingView OHLCV refresh..."
        );
        var result = await _refreshService.RefreshAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var s = result.Value;
            _logger.LogInformation(
                "[Quartz:TradingView] Completed in {ElapsedMs}ms ({Refreshed}/{Requested} tickers, {Quotes} quotes, Status: {Status}, Failed: {Failed}).",
                s.ElapsedMilliseconds,
                s.TickersRefreshed,
                s.TickersRequested,
                s.QuotesUpserted,
                s.Status,
                string.Join(',', s.FailedTickers)
            );
        }
        else
        {
            _logger.LogError("[Quartz:TradingView] Failed: {Error}", result.Error.Message);
        }
    }
}
