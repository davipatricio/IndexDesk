using IndexDesk.BuildingBlocks.Common.Results;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace IndexDesk.BuildingBlocks.Resilience;

public static class ResiliencePipelines
{
    public static ResiliencePipeline CreateDefaultHttpPipeline(int maxRetries = 3)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = maxRetries,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                }
            )
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }

    /// <summary>
    /// Provider-call pipeline: retry (exponential + jitter, honoring the
    /// <see cref="ProviderCallException.RetryAfter"/> hint when present) followed by a
    /// circuit breaker. Both strategies handle <see cref="ProviderCallException"/> only,
    /// with independent predicates — a failure can count toward the breaker without
    /// being worth an immediate retry (e.g. WAF blocks).
    ///
    /// The breaker state lives in the returned pipeline instance; callers cache it per
    /// provider name (e.g. <c>Modules.MarketData.Resilience.ProviderResilience</c>).
    /// "5 failures / 60 s" maps to MinimumThroughput + FailureRatio 1.0 over the
    /// sampling window; after <paramref name="breakerBreakDuration"/> Polly admits
    /// trial (half-open) calls and closes on success.
    /// </summary>
    public static ResiliencePipeline<T> CreateProviderCallPipeline<T>(
        int maxRetries,
        TimeSpan retryBaseDelay,
        Func<ProviderCallException, bool> retryPredicate,
        Func<ProviderCallException, bool> breakerPredicate,
        int breakerMinimumThroughput,
        double breakerFailureRatio,
        TimeSpan breakerSamplingDuration,
        TimeSpan breakerBreakDuration,
        TimeProvider? timeProvider = null
    )
    {
        var builder = new ResiliencePipelineBuilder<T>();
        if (timeProvider is not null)
        {
            builder.TimeProvider = timeProvider;
        }

        if (maxRetries > 0)
        {
            builder.AddRetry(
                new RetryStrategyOptions<T>
                {
                    MaxRetryAttempts = maxRetries,
                    Delay = retryBaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => new ValueTask<bool>(
                        IsSelected(args.Outcome.Exception, retryPredicate)
                    ),
                    // A server-advertised Retry-After overrides the exponential backoff.
                    DelayGenerator = args => new ValueTask<TimeSpan?>(
                        args.Outcome.Exception is ProviderCallException { RetryAfter: { } hint }
                            ? hint
                            : null
                    ),
                }
            );
        }

        builder.AddCircuitBreaker(
            new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = breakerFailureRatio,
                MinimumThroughput = breakerMinimumThroughput,
                SamplingDuration = breakerSamplingDuration,
                BreakDuration = breakerBreakDuration,
                ShouldHandle = args => new ValueTask<bool>(
                    IsSelected(args.Outcome.Exception, breakerPredicate)
                ),
            }
        );

        return builder.Build();
    }

    private static bool IsSelected(
        Exception? exception,
        Func<ProviderCallException, bool> predicate
    ) => exception is ProviderCallException callFailure && predicate(callFailure);
}
