using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Resilience;

/// <summary>
/// Thread-safe in-memory <see cref="IApiKeyPool"/> singleton (the Worker hosts a single
/// instance; horizontal scale-out would migrate counters to Redis). Each configured key
/// carries: a rotation state (cooldown after 429/quota, disable after 401/403) and a
/// token bucket sized by the provider's requests-per-minute budget — Acquire consumes
/// one token, so pacing happens inside the pool and clients stay simple.
///
/// Configuration (standard binding, secrets only from .env):
/// - <c>Providers__Brapi__ApiKeys__0..N</c> (legacy single <c>Providers__Brapi__ApiKey</c>
///   binds as index 0 when the array is empty);
/// - <c>Providers__AwesomeApi__Tokens__0..N</c> (legacy <c>Token</c>);
/// - <c>Providers__InfoMoney__SubscriptionKeys__0..N</c> (legacy <c>SubscriptionKey</c>);
/// - <c>Providers:{Provider}:RequestsPerMinutePerKey</c> — bucket size per key
///   (Brapi defaults to the free-plan 10/min/key, others to <see cref="DefaultRpm"/>).
/// </summary>
public sealed class InMemoryApiKeyPool : IApiKeyPool
{
    public const int DefaultRpm = 60;
    public const int DefaultBrapiRpm = 10;

    /// <summary>Fall-back cool-down for minute-based windows without Retry-After.</summary>
    public static readonly TimeSpan DefaultRateLimitCooldown = TimeSpan.FromSeconds(60);

    private static readonly Dictionary<string, (string ArrayKey, string LegacyKey)> KnownProviders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Brapi"] = ("ApiKeys", "ApiKey"),
            ["AwesomeApi"] = ("Tokens", "Token"),
            ["InfoMoney"] = ("SubscriptionKeys", "SubscriptionKey"),
        };

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryApiKeyPool> _logger;
    private readonly ConcurrentDictionary<string, ProviderState> _providers = new(
        StringComparer.OrdinalIgnoreCase
    );

    public InMemoryApiKeyPool(
        IConfiguration configuration,
        ILogger<InMemoryApiKeyPool> logger,
        TimeProvider? timeProvider = null
    )
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;

        foreach (var (provider, keys) in KnownProviders)
        {
            var section = configuration.GetSection($"Providers:{provider}");
            var values = section
                .GetSection(keys.ArrayKey)
                .GetChildren()
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .ToList();

            if (values.Count == 0)
            {
                // Legacy single-key config still works: treated as ApiKeys__0.
                var legacy = section[keys.LegacyKey];
                if (!string.IsNullOrWhiteSpace(legacy))
                {
                    values.Add(legacy.Trim());
                }
            }

            if (values.Count == 0)
            {
                continue; // provider runs keyless — HasKeys stays false
            }

            var rpm = ResolveRpm(section);
            var state = new ProviderState(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                state.Keys[i] = new KeyEntry(i, values[i], rpm, _timeProvider.GetUtcNow());
            }

            _providers[provider] = state;
            _logger.LogInformation(
                "[{Provider}] Key pool seeded with {Count} key(s) at {Rpm}/min/key.",
                provider,
                values.Count,
                rpm
            );
        }
    }

    /// <summary>Cool-down remaining until the next UTC day (daily-quota exhaustion).</summary>
    public static TimeSpan UntilNextUtcDay(DateTimeOffset now)
    {
        var nextDay = now.UtcDateTime.Date.AddDays(1);
        return nextDay - now;
    }

    public bool HasKeys(string provider) =>
        _providers.TryGetValue(provider, out var state) && Volatile.Read(ref state.Count) > 0;

    public string? Acquire(string provider)
    {
        if (!_providers.TryGetValue(provider, out var state))
        {
            return null;
        }

        lock (state.Gate)
        {
            var now = _timeProvider.GetUtcNow();
            var total = Volatile.Read(ref state.Count);
            for (var offset = 0; offset < total; offset++)
            {
                // Round-robin starts at the cursor so healthy keys share load evenly.
                var index = (state.Cursor + offset) % total;
                var entry = state.Keys[index];

                if (entry.DisabledSince is not null || entry.CooldownUntil > now)
                {
                    continue;
                }

                Refill(entry, now);
                if (entry.Tokens >= 1d)
                {
                    entry.Tokens -= 1d;
                    state.Cursor = (index + 1) % total;
                    return entry.Key;
                }
            }
        }

        _logger.LogWarning(
            "[{Provider}] Key pool exhausted (all keys cooling down, disabled or throttled).",
            provider
        );
        return null;
    }

    public void Report(string provider, string key, KeyResult result, TimeSpan? retryAfter = null)
    {
        if (!_providers.TryGetValue(provider, out var state))
        {
            return;
        }

        lock (state.Gate)
        {
            var entry = state.Keys.FirstOrDefault(e =>
                string.Equals(e.Key, key, StringComparison.Ordinal)
            );
            if (entry is null)
            {
                return;
            }

            switch (result)
            {
                case KeyResult.Success:
                    Interlocked.Increment(ref entry.Successes);
                    break;

                case KeyResult.RateLimited:
                    Interlocked.Increment(ref entry.RateLimiteds);
                    entry.CooldownUntil =
                        _timeProvider.GetUtcNow() + (retryAfter ?? DefaultRateLimitCooldown);
                    _logger.LogWarning(
                        "[{Provider}] Key #{Index} rate-limited; out of rotation until {Until:u}.",
                        provider,
                        entry.Index,
                        entry.CooldownUntil
                    );
                    break;

                case KeyResult.Invalid:
                    Interlocked.Increment(ref entry.Invalids);
                    entry.DisabledSince ??= _timeProvider.GetUtcNow();
                    // Alert-grade log line; surfaces as an error in sync_job_logs via the
                    // client's AuthFailed code. Index only — never the key value.
                    _logger.LogError(
                        "[{Provider}] Key #{Index} rejected as invalid (401/403); disabled until process restart.",
                        provider,
                        entry.Index
                    );
                    break;
            }
        }
    }

    public IReadOnlyList<ProviderKeyStats> Snapshot()
    {
        var stats = new List<ProviderKeyStats>();
        foreach (var (provider, state) in _providers)
        {
            lock (state.Gate)
            {
                var now = _timeProvider.GetUtcNow();
                foreach (var entry in state.Keys)
                {
                    var stateName =
                        entry.DisabledSince is not null ? "Disabled"
                        : entry.CooldownUntil > now ? "Cooldown"
                        : entry.Tokens < 1d ? "Throttled"
                        : "Healthy";

                    stats.Add(
                        new ProviderKeyStats(
                            provider,
                            entry.Index,
                            stateName,
                            Interlocked.Read(ref entry.Successes),
                            Interlocked.Read(ref entry.RateLimiteds),
                            Interlocked.Read(ref entry.Invalids)
                        )
                    );
                }
            }
        }

        return stats;
    }

    private static int ResolveRpm(IConfigurationSection section)
    {
        var rpm =
            int.TryParse(
                section["RequestsPerMinutePerKey"],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed
            )
                ? parsed
            : IsBrapi(section.Path) ? DefaultBrapiRpm
            : DefaultRpm;
        return Math.Max(1, rpm);
    }

    private static bool IsBrapi(string? path) =>
        path?.EndsWith("Providers:Brapi", StringComparison.OrdinalIgnoreCase) == true;

    private static void Refill(KeyEntry entry, DateTimeOffset now)
    {
        var elapsedMinutes = (now - entry.LastRefill).TotalMinutes;
        if (elapsedMinutes <= 0)
        {
            return;
        }

        entry.Tokens = Math.Min(entry.Capacity, entry.Tokens + elapsedMinutes * entry.Capacity);
        entry.LastRefill = now;
    }

    private sealed class ProviderState(int capacityHint)
    {
        public readonly object Gate = new();
        public readonly KeyEntry[] Keys = new KeyEntry[Math.Max(1, capacityHint)];
        public int Cursor;
        public int Count = Math.Max(0, capacityHint);
    }

    private sealed class KeyEntry
    {
        public KeyEntry(int index, string key, double rpm, DateTimeOffset now)
        {
            Index = index;
            Key = key;
            Capacity = rpm;
            Tokens = rpm; // start with a full bucket
            LastRefill = now;
        }

        public int Index { get; }
        public string Key { get; }
        public double Capacity { get; }
        public double Tokens;
        public DateTimeOffset LastRefill;
        public DateTimeOffset CooldownUntil;
        public DateTimeOffset? DisabledSince;
        public long Successes;
        public long RateLimiteds;
        public long Invalids;
    }
}
