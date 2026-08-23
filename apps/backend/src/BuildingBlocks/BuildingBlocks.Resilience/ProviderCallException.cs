using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.BuildingBlocks.Resilience;

/// <summary>
/// Bridges <see cref="Result{T}"/>-based provider calls into Polly strategies: callers
/// translate a failed <c>Result</c> into this exception so retry/circuit-breaker
/// pipelines can react, and convert it back to <c>Result</c> at the boundary.
/// Carries an optional <c>Retry-After</c> hint extracted from the provider response.
/// </summary>
public sealed class ProviderCallException : Exception
{
    public ProviderCallException(Error error, TimeSpan? retryAfter = null)
        : base(error.Message)
    {
        ErrorCode = error.Code;
        RetryAfter = retryAfter;
    }

    /// <summary>The original error code (e.g. <c>Brapi.HttpError</c>).</summary>
    public string ErrorCode { get; }

    /// <summary>Server-advertised cool-down (<c>Retry-After</c> header), when present.</summary>
    public TimeSpan? RetryAfter { get; }
}
