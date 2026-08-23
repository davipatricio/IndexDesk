namespace IndexDesk.Modules.MarketData.Resilience;

/// <summary>Outcome reported back to the pool after one attempt with an acquired key.</summary>
public enum KeyResult
{
    /// <summary>The call completed (any HTTP 2xx mapped to data).</summary>
    Success,

    /// <summary>The provider rate-limited the key (429 or quota-exhausted body).</summary>
    RateLimited,

    /// <summary>The provider rejected the credential itself (401/403).</summary>
    Invalid,
}

/// <summary>
/// Per-provider rotation of API keys/tokens whose limits are enforced **per key**
/// (Brapi free/paid, AwesomeAPI token, InfoMoney APIM). Round-robin among healthy
/// keys only; when no healthy key exists <see cref="Acquire"/> returns null and the
/// caller falls through to the next provider of the chain (never a hard error).
/// Keys are identified by INDEX in logs/metrics — never by value.
/// </summary>
public interface IApiKeyPool
{
    /// <summary>
    /// Picks the next healthy key for the provider (round-robin), consuming one token
    /// from its per-key request-per-minute bucket. Returns null when the provider has
    /// keys configured but every one is cooling down, disabled or throttled.
    /// </summary>
    string? Acquire(string provider);

    /// <summary>
    /// Reports the outcome of using <paramref name="key"/>.
    /// <see cref="KeyResult.RateLimited"/> removes the key from rotation until the end
    /// of the limit window — <paramref name="retryAfter"/> when advertised, otherwise a
    /// 60 s default for minute-based windows; daily-quota exhaustion passes a computed
    /// window (e.g. until the next UTC day). <see cref="KeyResult.Invalid"/> disables
    /// the key until process restart.
    /// </summary>
    void Report(string provider, string key, KeyResult result, TimeSpan? retryAfter = null);

    /// <summary>True when at least one key is configured for the provider. Providers
    /// without any configured key run keyless (legacy behavior) instead of failing.</summary>
    bool HasKeys(string provider);

    /// <summary>Observability snapshot per provider/key index — counters only, no secrets.</summary>
    IReadOnlyList<ProviderKeyStats> Snapshot();
}

/// <summary>Health/counters of one key, identified by index only.</summary>
public sealed record ProviderKeyStats(
    string Provider,
    int KeyIndex,
    string State, // Healthy | Cooldown | Disabled | Throttled
    long SuccessCount,
    long RateLimitedCount,
    long InvalidCount
);
