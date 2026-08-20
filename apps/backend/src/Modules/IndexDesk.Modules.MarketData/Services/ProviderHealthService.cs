using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Health;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.MarketData.Services;

public class ProviderHealthService : IProviderHealthService
{
    private readonly IndexDeskDbContext _dbContext;

    public ProviderHealthService(IndexDeskDbContext dbContext) => _dbContext = dbContext;

    public async Task<ProviderHealthResponseDto> GetProvidersHealthAsync(
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _dbContext.SyncJobLogs.AsNoTracking().ToListAsync(cancellationToken);

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        var status = ProviderHealthAggregator.OverallStatus(providers);

        var summary = new ProviderHealthSummaryDto(
            Total: providers.Count,
            Healthy: providers.Count(p => p.Status == "HEALTHY"),
            Degraded: providers.Count(p => p.Status == "DEGRADED"),
            Unhealthy: providers.Count(p => p.Status == "UNHEALTHY"),
            NeverSynced: providers.Count(p => p.Status == "NEVER_SYNCED")
        );

        return new ProviderHealthResponseDto(
            Status: status,
            GeneratedAt: DateTimeOffset.UtcNow,
            Summary: summary,
            Providers: providers
        );
    }
}
