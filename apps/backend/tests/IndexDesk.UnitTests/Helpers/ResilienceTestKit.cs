using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndexDesk.UnitTests.Helpers;

/// <summary>Hand-rolled deterministic clock (no sleeps): advance time explicitly.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan time) => _utcNow += time;
}

/// <summary>Factory seams for Fase-4 resilience types (offline, deterministic defaults).</summary>
public static class ResilienceTestKit
{
    /// <summary>Pool bound over an in-memory configuration snapshot.</summary>
    public static InMemoryApiKeyPool NewPool(
        Dictionary<string, string?>? settings = null,
        FakeTimeProvider? clock = null
    )
    {
        var config = settings is null
            ? new ConfigurationBuilder().Build()
            : new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new InMemoryApiKeyPool(
            config,
            NullLogger<InMemoryApiKeyPool>.Instance,
            clock ?? TimeProvider.System
        );
    }

    /// <summary>Pool sharing the exact configuration a test builds for its client.</summary>
    public static InMemoryApiKeyPool NewPoolFromConfig(
        IConfiguration config,
        FakeTimeProvider? clock = null
    ) => new(config, NullLogger<InMemoryApiKeyPool>.Instance, clock ?? TimeProvider.System);

    /// <summary>Resilience with retries disabled by default so attempt counts stay
    /// deterministic; pass explicit options for retry/breaker scenarios.</summary>
    public static ProviderResilience NewResilience(ProviderResilienceOptions? options = null)
    {
        var effective =
            options ?? new ProviderResilienceOptions { HttpMaxRetries = 0, SidecarMaxRetries = 0 };
        return new ProviderResilience(effective, NullLogger<ProviderResilience>.Instance);
    }
}
