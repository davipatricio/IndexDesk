namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class SyncJobLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS"; // SUCCESS, FAILED, PARTIAL_WARNING
    public int RecordsProcessed { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsSkipped { get; set; }
    public string? ErrorDetails { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
