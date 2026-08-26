using IndexDesk.Modules.Portfolio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// Persists the latest daily valuation of every non-empty portfolio into
/// portfolio_daily_snapshots (hypertable). Runs after the market data/FX syncs so it
/// always sees fresh local prices; re-execution is an idempotent upsert per (portfolio, day).
/// </summary>
[DisallowConcurrentExecution]
public sealed class PortfolioSnapshotDailyJob(
    IServiceScopeFactory scopeFactory,
    ILogger<PortfolioSnapshotDailyJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[Quartz] Persisting daily portfolio snapshots...");

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPortfolioPerformanceService>();

        try
        {
            var count = await service.SnapshotAllPortfoliosAsync(context.CancellationToken);
            logger.LogInformation(
                "[Quartz] Portfolio snapshots persisted for {Count} portfolios.",
                count
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[Quartz] Portfolio snapshot job failed.");
            throw;
        }
    }
}
