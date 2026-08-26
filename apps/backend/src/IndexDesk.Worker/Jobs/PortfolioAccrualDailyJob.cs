using IndexDesk.Modules.Portfolio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

/// <summary>
/// Recomputa o accrual de todos os parâmetros de renda fixa (CDI%/CDI+/Selic/IPCA+/Prefixado)
/// com as séries macro locais. Upsert por (param, dia): re-execução no mesmo dia não muda valor
/// — idempotência auditável via last_accrual_date. Roda após MarketData/FX e antes do snapshot.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PortfolioAccrualDailyJob(
    IServiceScopeFactory scopeFactory,
    ILogger<PortfolioAccrualDailyJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("[Quartz] Running fixed-income accrual pass...");

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPortfolioFixedIncomeService>();

        try
        {
            var count = await service.AccrueAllAsync(context.CancellationToken);
            logger.LogInformation(
                "[Quartz] Fixed-income accrual updated {Count} parameter rows.",
                count
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[Quartz] Portfolio accrual job failed.");
            throw;
        }
    }
}
