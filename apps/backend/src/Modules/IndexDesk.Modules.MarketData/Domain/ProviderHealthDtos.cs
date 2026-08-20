namespace IndexDesk.Modules.MarketData.Domain;

public readonly record struct ProviderHealthResponseDto(
    string Status, // UNKNOWN | HEALTHY | DEGRADED | UNHEALTHY
    DateTimeOffset GeneratedAt,
    ProviderHealthSummaryDto Summary,
    IReadOnlyList<ProviderHealthDto> Providers
);

public readonly record struct ProviderHealthSummaryDto(
    int Total,
    int Healthy,
    int Degraded,
    int Unhealthy,
    int NeverSynced
);

public readonly record struct ProviderHealthDto(
    string Provider,
    string DisplayName,
    string Role,
    string Status, // NEVER_SYNCED | HEALTHY | DEGRADED | UNHEALTHY
    ProviderLastSyncDto? LastSync,
    ProviderTotalsDto Totals,
    IReadOnlyList<SyncIssueDto> Issues
);

public readonly record struct ProviderLastSyncDto(
    string JobName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int ExecutionTimeMs,
    int RecordsProcessed,
    int RecordsUpdated
);

public readonly record struct ProviderTotalsDto(
    int Jobs,
    int RecordsProcessed,
    int RecordsUpdated,
    int RecordsSkipped,
    int SuccessCount,
    int WarningCount,
    int ErrorCount
);

public readonly record struct SyncIssueDto(
    DateTimeOffset At,
    string JobName,
    string Status,
    string? Message,
    int ExecutionTimeMs
);
