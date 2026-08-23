using System.Collections.Concurrent;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace IndexDesk.Modules.MarketData.Resilience;

/// <summary>Which failure profile a provider call uses.</summary>
public enum ProviderMode
{
    /// <summary>Native HTTP providers (Brapi, AwesomeApi): exponential+jitter retries.</summary>
    Http,

    /// <summary>Python sidecar processes: process timeout already enforced by the runner
    /// (120 s default); exactly one extra attempt on Timeout/FetchFailed.</summary>
    Sidecar,
}

/// <summary>Tunable resilience knobs (bound from <c>Providers:Resilience:*</c> and
/// <c>Providers:Sidecar:MaxRetries</c>).</summary>
public sealed class ProviderResilienceOptions
{
    public int HttpMaxRetries { get; init; } = 2;
    public TimeSpan HttpRetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public int SidecarMaxRetries { get; init; } = 1;

    /// <summary>Minimum failures inside the sampling window before the breaker can open.</summary>
    public int BreakerFailures { get; init; } = 5;

    public double BreakerFailureRatio { get; init; } = 1.0;
    public TimeSpan BreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan BreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    public static ProviderResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Providers:Resilience");
        int GetInt(string key, int fallback) =>
            int.TryParse(section[key], out var parsed) && parsed >= 0 ? parsed : fallback;

        var sidecarMaxRetries =
            int.TryParse(configuration["Providers:Sidecar:MaxRetries"], out var sidecar)
            && sidecar >= 0
                ? sidecar
                : 1;

        return new ProviderResilienceOptions
        {
            HttpMaxRetries = GetInt("HttpMaxRetries", 2),
            HttpRetryBaseDelay = TimeSpan.FromMilliseconds(GetInt("HttpRetryBaseDelayMs", 250)),
            SidecarMaxRetries = sidecarMaxRetries,
            BreakerFailures = Math.Max(1, GetInt("BreakerFailures", 5)),
            BreakerSamplingDuration = TimeSpan.FromSeconds(
                Math.Max(1, GetInt("BreakerSamplingSeconds", 60))
            ),
            BreakerBreakDuration = TimeSpan.FromSeconds(
                Math.Max(0, GetInt("BreakerBreakSeconds", 30))
            ),
        };
    }
}

/// <summary>
/// Per-provider retry + circuit-breaker orchestration for <see cref="Result{T}"/>-based
/// provider calls (Fase 4 — plans/provider-sync-scrapers.md).
///
/// - Retry: exponential + jitter; HTTP providers 2 attempts, sidecars 1 (Timeout /
///   FetchFailed only). A server-advertised Retry-After would override the delay.
/// - Breaker: state is cached **per provider name** (5 failures / 60 s sampling,
///   half-open trial after the break duration). When open, calls short-circuit to
///   <c>Provider.CircuitOpen</c> so the chain moves to the next provider instead of
///   hanging.
///
/// Failure taxonomy (error-code based):
/// - retried + counted: *.HttpError, *.Exception, *.RateLimit (retry only),
///   Sidecar.Timeout, Sidecar.FetchFailed, Scrape.WafBlocked (breaker only),
///   *.AuthFailed (breaker only — persistent rejection must open the breaker);
/// - passed through untouched (no retry, no breaker count): *.NoApiKey,
///   *.NoData, PoolExhausted, Sidecar.ParseError/Usage/SpawnFailed.
/// </summary>
public sealed class ProviderResilience
{
    private readonly ConcurrentDictionary<
        (string Provider, Type Type, ProviderMode Mode),
        object
    > _pipelines = new();
    private readonly ProviderResilienceOptions _options;
    private readonly ILogger<ProviderResilience> _logger;

    public ProviderResilience(IConfiguration configuration, ILogger<ProviderResilience> logger)
        : this(ProviderResilienceOptions.FromConfiguration(configuration), logger) { }

    internal ProviderResilience(
        ProviderResilienceOptions options,
        ILogger<ProviderResilience> logger
    )
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>Runs <paramref name="call"/> under the provider's pipeline. Never throws
    /// for provider failures — they come back as failed Results with their original codes
    /// (<c>Provider.CircuitOpen</c> when the breaker is open).</summary>
    public async Task<Result<T>> ExecuteAsync<T>(
        string provider,
        string operation,
        Func<Task<Result<T>>> call,
        CancellationToken cancellationToken = default,
        ProviderMode mode = ProviderMode.Http
    )
    {
        var pipeline = GetPipeline<T>(provider, mode);
        try
        {
            return await pipeline
                .ExecuteAsync(
                    async token =>
                    {
                        var result = await call().ConfigureAwait(false);
                        if (result.IsFailure && CountsTowardBreaker(provider, result.Error.Code))
                        {
                            throw new ProviderCallException(result.Error);
                        }

                        return result;
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning(
                "[{Provider}] Circuit open — short-circuiting {Operation} without calling the provider.",
                provider,
                operation
            );
            return Result<T>.Failure(
                Error.Failure(
                    "Provider.CircuitOpen",
                    $"{provider} circuit breaker is open; skipping {operation} until the half-open probe succeeds"
                )
            );
        }
        catch (ProviderCallException ex)
        {
            // Retries exhausted (or strategy declined the code): restore the original error.
            return Result<T>.Failure(Error.Failure(ex.ErrorCode, ex.Message));
        }
    }

    /// <summary>Wraps a raw transport exception so retry/breaker strategies see it.</summary>
    public static ProviderCallException AsProviderCall(string provider, Exception exception) =>
        new(Error.Failure($"{provider}.Exception", exception.Message));

    private ResiliencePipeline<Result<T>> GetPipeline<T>(string provider, ProviderMode mode)
    {
        var key = (provider.ToUpperInvariant(), typeof(T), mode);
        return (ResiliencePipeline<Result<T>>)
            _pipelines.GetOrAdd(
                key,
                static (_, context) =>
                    context.self.BuildPipeline<T>(context.provider, context.mode),
                (self: this, provider, mode)
            );
    }

    private ResiliencePipeline<Result<T>> BuildPipeline<T>(string provider, ProviderMode mode)
    {
        var maxRetries =
            mode == ProviderMode.Http ? _options.HttpMaxRetries : _options.SidecarMaxRetries;

        return ResiliencePipelines.CreateProviderCallPipeline<Result<T>>(
            maxRetries,
            _options.HttpRetryBaseDelay,
            retryPredicate: ex => IsTransient(provider, ex.ErrorCode, mode),
            breakerPredicate: ex => CountsTowardBreaker(provider, ex.ErrorCode),
            _options.BreakerFailures,
            _options.BreakerFailureRatio,
            _options.BreakerSamplingDuration,
            _options.BreakerBreakDuration,
            timeProvider: null
        );
    }

    private static bool IsTransient(string provider, string errorCode, ProviderMode mode)
    {
        // Rate-limited responses are retried (the pool rotates to another healthy key or
        // fails fast when exhausted), but they do NOT open the breaker — the per-key
        // cooldown owns that failure mode.
        if (errorCode.EndsWith(".RateLimit", StringComparison.Ordinal))
        {
            return true;
        }

        if (!CountsTowardBreaker(provider, errorCode))
        {
            return false;
        }

        // WAF blocks and auth rejections open the breaker but are not worth an immediate
        // blind retry against the same wall.
        if (
            errorCode.Equals("Scrape.WafBlocked", StringComparison.Ordinal)
            || errorCode.EndsWith(".AuthFailed", StringComparison.Ordinal)
        )
        {
            return false;
        }

        // Sidecar spawn/usage/parse problems are environmental, not transient.
        if (mode == ProviderMode.Sidecar)
        {
            return errorCode is "Sidecar.Timeout" or "Sidecar.FetchFailed";
        }

        return errorCode.EndsWith(".HttpError", StringComparison.Ordinal)
            || errorCode.EndsWith(".Exception", StringComparison.Ordinal);
    }

    private static bool CountsTowardBreaker(string provider, string errorCode)
    {
        if (
            errorCode.EndsWith(".RateLimit", StringComparison.Ordinal)
            || errorCode.EndsWith(".NoApiKey", StringComparison.Ordinal)
            || errorCode.EndsWith(".NoData", StringComparison.Ordinal)
            || errorCode.EndsWith(".NotFound", StringComparison.Ordinal)
        )
        {
            return false;
        }

        return errorCode.EndsWith(".HttpError", StringComparison.Ordinal)
            || errorCode.EndsWith(".Exception", StringComparison.Ordinal)
            || errorCode.EndsWith(".AuthFailed", StringComparison.Ordinal)
            || errorCode is "Scrape.WafBlocked" or "Sidecar.Timeout" or "Sidecar.FetchFailed";
    }
}
