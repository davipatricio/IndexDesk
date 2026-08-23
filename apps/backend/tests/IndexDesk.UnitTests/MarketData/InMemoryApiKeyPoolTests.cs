using FluentAssertions;
using IndexDesk.Modules.MarketData.Resilience;
using IndexDesk.UnitTests.Helpers;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the Fase-4 API-key pool: round-robin, cooldown after 429
/// (Retry-After honored), Invalid isolated until restart, per-key token bucket with a
/// fake clock, legacy single-key binding and pool-exhaustion → null (soft fallback).
/// </summary>
public class InMemoryApiKeyPoolTests
{
    private static Dictionary<string, string?> ThreeKeys() =>
        new()
        {
            ["Providers:Brapi:ApiKeys:0"] = "key_a",
            ["Providers:Brapi:ApiKeys:1"] = "key_b",
            ["Providers:Brapi:ApiKeys:2"] = "key_c",
        };

    [Fact]
    public void Acquire_RoundRobin_CyclesThroughHealthyKeysInOrder()
    {
        var clock = new FakeTimeProvider();
        var pool = ResilienceTestKit.NewPool(ThreeKeys(), clock);

        var sequence = Enumerable.Range(0, 6).Select(_ => pool.Acquire("Brapi")).ToList();

        // Bucket capacity = rpm; default Brapi rpm 10 → 3 keys hold 30 tokens, so six
        // acquires fit comfortably and must follow the strict rotation order.
        sequence.Should().Equal("key_a", "key_b", "key_c", "key_a", "key_b", "key_c");
    }

    [Fact]
    public void Report_RateLimited_KeyOutOfRotationForDefault60Seconds()
    {
        var clock = new FakeTimeProvider();
        var pool = ResilienceTestKit.NewPool(ThreeKeys(), clock);

        pool.Report("Brapi", "key_b", KeyResult.RateLimited);

        pool.Acquire("Brapi").Should().Be("key_a"); // healthy keys keep rotating
        clock.Advance(TimeSpan.FromSeconds(59));
        pool.Acquire("Brapi").Should().NotBe("key_b");
        clock.Advance(TimeSpan.FromSeconds(2)); // past the 60 s window
        Enumerable.Range(0, 3).Select(_ => pool.Acquire("Brapi")).Should().Contain("key_b"); // back in rotation once the window ends
    }

    [Fact]
    public void Report_RateLimited_WithRetryAfter_HonorsTheLongerWindow()
    {
        var clock = new FakeTimeProvider();
        var pool = ResilienceTestKit.NewPool(ThreeKeys(), clock);

        pool.Report("Brapi", "key_a", KeyResult.RateLimited, retryAfter: TimeSpan.FromSeconds(120));

        clock.Advance(TimeSpan.FromSeconds(61)); // default window would be over…
        pool.Acquire("Brapi").Should().NotBe("key_a"); // …but Retry-After rules
        clock.Advance(TimeSpan.FromSeconds(59));
        Enumerable.Range(0, 3).Select(_ => pool.Acquire("Brapi")).Should().Contain("key_a"); // released after the advertised window
    }

    [Fact]
    public void Report_Invalid_DisabledUntilRestart_IgnoredByClockAdvances()
    {
        var clock = new FakeTimeProvider();
        var pool = ResilienceTestKit.NewPool(ThreeKeys(), clock);

        pool.Report("Brapi", "key_a", KeyResult.Invalid);

        pool.Acquire("Brapi").Should().Be("key_b");
        clock.Advance(TimeSpan.FromDays(30));
        pool.Acquire("Brapi").Should().NotBe("key_a"); // still disabled

        var stats = pool.Snapshot().Single(s => s.Provider == "Brapi" && s.KeyIndex == 0);
        stats.State.Should().Be("Disabled");
        stats.InvalidCount.Should().Be(1);
    }

    [Fact]
    public void Acquire_AllKeysExhausted_ReturnsNull_SoftFallbackSignal()
    {
        var clock = new FakeTimeProvider();
        var settings = ThreeKeys();
        settings["Providers:Brapi:ApiKeys:2"] = null; // only two real keys
        var pool = ResilienceTestKit.NewPool(settings, clock);

        pool.Report("Brapi", "key_a", KeyResult.RateLimited);
        pool.Report("Brapi", "key_b", KeyResult.RateLimited);

        pool.Acquire("Brapi").Should().BeNull();

        clock.Advance(TimeSpan.FromSeconds(61));
        pool.Acquire("Brapi").Should().NotBeNull(); // cooldown expired → healthy again
    }

    [Fact]
    public void TokenBucket_LimitsRequestsPerMinutePerKey()
    {
        var clock = new FakeTimeProvider();
        var settings = new Dictionary<string, string?>
        {
            ["Providers:Brapi:ApiKeys:0"] = "solo",
            ["Providers:Brapi:RequestsPerMinutePerKey"] = "3",
        };
        var pool = ResilienceTestKit.NewPool(settings, clock);

        // Capacity 3: the fourth acquire inside the same instant finds the bucket dry.
        pool.Acquire("Brapi").Should().Be("solo");
        pool.Acquire("Brapi").Should().Be("solo");
        pool.Acquire("Brapi").Should().Be("solo");
        pool.Acquire("Brapi").Should().BeNull();

        var stats = pool.Snapshot().Single();
        stats.State.Should().Be("Throttled");

        clock.Advance(TimeSpan.FromSeconds(20)); // refills exactly one token at 3/min
        pool.Acquire("Brapi").Should().Be("solo");
        pool.Acquire("Brapi").Should().BeNull(); // next token needs another 20 s
    }

    [Fact]
    public void TokenBucket_RefillsProportionallyToElapsedTime()
    {
        var clock = new FakeTimeProvider();
        var settings = new Dictionary<string, string?>
        {
            ["Providers:Brapi:ApiKeys:0"] = "solo",
            ["Providers:Brapi:RequestsPerMinutePerKey"] = "10",
        };
        var pool = ResilienceTestKit.NewPool(settings, clock);

        for (var i = 0; i < 10; i++)
        {
            pool.Acquire("Brapi").Should().Be("solo");
        }

        clock.Advance(TimeSpan.FromSeconds(6)); // 6/60 of a minute × 10 rpm = 1 token
        pool.Acquire("Brapi").Should().Be("solo");

        clock.Advance(TimeSpan.FromSeconds(3)); // half a token — not enough yet
        pool.Acquire("Brapi").Should().BeNull();
    }

    [Fact]
    public void LegacySingleApiKey_BindsAsIndexZero()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:Brapi:ApiKey"] = "legacy_only" }
        );

        pool.HasKeys("Brapi").Should().BeTrue();
        pool.Acquire("Brapi").Should().Be("legacy_only");

        var stats = pool.Snapshot().Single(s => s.Provider == "Brapi");
        stats.KeyIndex.Should().Be(0);
    }

    [Fact]
    public void UnconfiguredProvider_HasNoKeys_AndAcquireReturnsNull()
    {
        var pool = ResilienceTestKit.NewPool(ThreeKeys());

        pool.HasKeys("YahooSidecar").Should().BeFalse(); // IP-limited sources don't rotate
        pool.Acquire("YahooSidecar").Should().BeNull();
        pool.HasKeys("AwesomeApi").Should().BeFalse(); // Tokens array absent
    }

    [Fact]
    public void Snapshot_NeverExposesKeyValues()
    {
        var pool = ResilienceTestKit.NewPool(ThreeKeys());
        pool.Report("Brapi", "key_b", KeyResult.Success);

        var snapshot = pool.Snapshot().Where(s => s.Provider == "Brapi").ToList();

        snapshot.Should().HaveCount(3);
        snapshot.Select(s => s.KeyIndex).Should().Equal(0, 1, 2);
        // The record carries indexes/counters only — assert no field leaks raw material.
        snapshot
            .SelectMany(s =>
                new[]
                {
                    s.Provider,
                    s.State,
                    s.KeyIndex.ToString(),
                    s.SuccessCount.ToString(),
                    s.RateLimitedCount.ToString(),
                    s.InvalidCount.ToString(),
                }
            )
            .Should()
            .NotContain(v => v.Contains("key_a") || v.Contains("key_b") || v.Contains("key_c"));
    }

    [Fact]
    public void Report_ForUnknownProviderOrUnknownKey_IsIgnoredGracefully()
    {
        var pool = ResilienceTestKit.NewPool(ThreeKeys());

        var act = () =>
        {
            pool.Report("YahooSidecar", "nope", KeyResult.RateLimited);
            pool.Report("Brapi", "unknown_key", KeyResult.Invalid);
        };

        act.Should().NotThrow();
        pool.Snapshot()
            .Single(s => s.Provider == "Brapi" && s.KeyIndex == 0)
            .State.Should()
            .Be("Healthy");
    }

    [Fact]
    public void UntilNextUtcDay_ComputesRemainingWindowToMidnight()
    {
        var now = new DateTimeOffset(2026, 8, 23, 23, 30, 0, TimeSpan.Zero);
        InMemoryApiKeyPool.UntilNextUtcDay(now).Should().Be(TimeSpan.FromMinutes(30));

        var noon = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        InMemoryApiKeyPool.UntilNextUtcDay(noon).Should().Be(TimeSpan.FromHours(12));
    }
}
