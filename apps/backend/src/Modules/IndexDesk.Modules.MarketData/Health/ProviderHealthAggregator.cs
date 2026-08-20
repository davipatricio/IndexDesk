using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Health;

internal sealed record ProviderDescriptor(string Key, string DisplayName, string Role);

/// <summary>
/// Pure aggregation over sync_job_logs rows. No I/O — kept static so the
/// status derivation rules are directly unit-testable without a database.
/// </summary>
internal static class ProviderHealthAggregator
{
    private static readonly IReadOnlyList<ProviderDescriptor> KnownProviders = new[]
    {
        new ProviderDescriptor("Brapi", "Brapi.dev", "Cotações B3 e proventos (data COM/EX)"),
        new ProviderDescriptor(
            "YahooFinance",
            "Yahoo Finance",
            "Benchmarks globais e fallback de cotações"
        ),
        new ProviderDescriptor("HGBrasil", "HG Brasil", "Contingência (cotações)"),
        new ProviderDescriptor(
            "BCB",
            "Banco Central do Brasil (SGS)",
            "Séries macro: CDI, Selic, IPCA, IGP-M"
        ),
        new ProviderDescriptor("CVM", "CVM", "Informe diário, PL/cotistas e holdings"),
    };

    private const string NeverSynced = "NEVER_SYNCED";
    private const string Healthy = "HEALTHY";
    private const string Degraded = "DEGRADED";
    private const string Unhealthy = "UNHEALTHY";
    private const string Unknown = "UNKNOWN";

    public static IReadOnlyList<ProviderHealthDto> BuildProviders(
        IReadOnlyList<SyncJobLogEntity> logs
    )
    {
        var providerLogs = logs.GroupBy(l => l.ProviderName)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.StartedAt).ToList());

        // Known providers first (stable contract), then any catalogued-in-logs provider.
        var keys = KnownProviders
            .Select(p => p.Key)
            .Concat(providerLogs.Keys.Where(k => KnownProviders.All(p => p.Key != k)))
            .Distinct()
            .ToList();

        return keys.Select(key =>
                BuildProvider(
                    key,
                    providerLogs.TryGetValue(key, out var list)
                        ? list
                        : new List<SyncJobLogEntity>()
                )
            )
            .ToList();
    }

    public static string OverallStatus(IReadOnlyList<ProviderHealthDto> providers)
    {
        if (providers.All(p => p.Status == NeverSynced))
            return Unknown;
        if (providers.Any(p => p.Status == Unhealthy))
            return Unhealthy;
        if (providers.Any(p => p.Status == Degraded))
            return Degraded;
        return Healthy;
    }

    private static ProviderHealthDto BuildProvider(string key, IReadOnlyList<SyncJobLogEntity> logs)
    {
        var descriptor = KnownProviders.FirstOrDefault(p => p.Key == key);
        var (displayName, role) = descriptor is not null
            ? (descriptor.DisplayName, descriptor.Role)
            : DescribeUnknown(key);

        if (logs.Count == 0)
        {
            return new ProviderHealthDto(
                Provider: key,
                DisplayName: displayName,
                Role: role,
                Status: NeverSynced,
                LastSync: null,
                Totals: new ProviderTotalsDto(
                    Jobs: 0,
                    RecordsProcessed: 0,
                    RecordsUpdated: 0,
                    RecordsSkipped: 0,
                    SuccessCount: 0,
                    WarningCount: 0,
                    ErrorCount: 0
                ),
                Issues: Array.Empty<SyncIssueDto>()
            );
        }

        var latest = logs[0]; // already ordered StartedAt desc
        var status = latest.Status switch
        {
            "FAILED" => Unhealthy,
            "PARTIAL_WARNING" => Degraded,
            _ => Healthy,
        };

        var issues = logs.Where(l => l.Status is "FAILED" or "PARTIAL_WARNING")
            .Take(20)
            .Select(l => new SyncIssueDto(
                At: l.StartedAt,
                JobName: l.JobName,
                Status: l.Status,
                Message: l.ErrorDetails,
                ExecutionTimeMs: l.ExecutionTimeMs
            ))
            .ToList();

        return new ProviderHealthDto(
            Provider: key,
            DisplayName: displayName,
            Role: role,
            Status: status,
            LastSync: new ProviderLastSyncDto(
                JobName: latest.JobName,
                Status: latest.Status,
                StartedAt: latest.StartedAt,
                CompletedAt: latest.CompletedAt,
                ExecutionTimeMs: latest.ExecutionTimeMs,
                RecordsProcessed: latest.RecordsProcessed,
                RecordsUpdated: latest.RecordsUpdated
            ),
            Totals: new ProviderTotalsDto(
                Jobs: logs.Count,
                RecordsProcessed: logs.Sum(l => l.RecordsProcessed),
                RecordsUpdated: logs.Sum(l => l.RecordsUpdated),
                RecordsSkipped: logs.Sum(l => l.RecordsSkipped),
                SuccessCount: logs.Count(l => l.Status == "SUCCESS"),
                WarningCount: logs.Count(l => l.Status == "PARTIAL_WARNING"),
                ErrorCount: logs.Count(l => l.Status == "FAILED")
            ),
            Issues: issues
        );
    }

    private static (string DisplayName, string Role) DescribeUnknown(string key) =>
        key switch
        {
            "ALL" => ("Pipeline de fallback", "Nenhum provedor respondeu (falha total)"),
            "UNKNOWN" => ("Erro interno", "Falha não atribuída a provedor específico"),
            _ => (key, "Provedor não catalogado"),
        };
}
