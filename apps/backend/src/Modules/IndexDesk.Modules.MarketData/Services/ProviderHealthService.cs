using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Health;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.MarketData.Services;

public class ProviderHealthService : IProviderHealthService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IApiKeyPool _apiKeyPool;

    public ProviderHealthService(IndexDeskDbContext dbContext, IApiKeyPool apiKeyPool)
    {
        _dbContext = dbContext;
        _apiKeyPool = apiKeyPool;
    }

    public async Task<ProviderHealthResponseDto> GetProvidersHealthAsync(
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _dbContext.SyncJobLogs.AsNoTracking().ToListAsync(cancellationToken);

        var keyStats = _apiKeyPool
            .Snapshot()
            .GroupBy(s => s.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyList<ProviderKeyHealthDto>)
                        g.Select(s => new ProviderKeyHealthDto(
                                s.KeyIndex,
                                s.State,
                                s.SuccessCount,
                                s.RateLimitedCount,
                                s.InvalidCount
                            ))
                            .ToList()
            );

        var providers = ProviderHealthAggregator.BuildProviders(logs, keyStats);
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
