using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed class PilotAssetBackfillJob : IJob
{
    private readonly IAssetBackfillService _backfillService;
    private readonly ILogger<PilotAssetBackfillJob> _logger;

    public PilotAssetBackfillJob(
        IAssetBackfillService backfillService,
        ILogger<PilotAssetBackfillJob> logger
    )
    {
        _backfillService = backfillService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz:PilotBackfill] Starting on-demand backfill for pilot assets (MXRF11, VWRA11, GOLD11)..."
        );
        var result = await _backfillService.BackfillPilotAssetsAsync(
            cancellationToken: context.CancellationToken
        );

        if (result.IsSuccess)
        {
            var summaries = result.Value;
            foreach (var summary in summaries)
            {
                _logger.LogInformation(
                    "[Quartz:PilotBackfill] {Ticker} -> Status: {Status}, Quotes: {Quotes}, Dividends: {Dividends}, Provider: {Provider}, Time: {TimeMs}ms",
                    summary.Ticker,
                    summary.Status,
                    summary.QuotesIngested,
                    summary.DividendsIngested,
                    summary.SourceProvider,
                    summary.ElapsedMilliseconds
                );
            }
        }
        else
        {
            _logger.LogError("[Quartz:PilotBackfill] Failed: {Error}", result.Error.Message);
        }
    }
}
