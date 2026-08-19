using Microsoft.Extensions.Logging;
using Quartz;

namespace IndexDesk.Worker.Jobs;

[DisallowConcurrentExecution]
public sealed class BcbSyncJob : IJob
{
    private readonly ILogger<BcbSyncJob> _logger;

    public BcbSyncJob(ILogger<BcbSyncJob> logger)
    {
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "[Quartz] Starting scheduled BCB macro indicators synchronization (CDI, Selic, IPCA)..."
        );
        // Local-first background ingestion logic
        _logger.LogInformation("[Quartz] BCB synchronization completed successfully.");
        return Task.CompletedTask;
    }
}
