using Microsoft.Extensions.Configuration;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Pure decision rules for the daily-close chain status (extracted from
/// <see cref="DailyCloseSyncService"/> in Fase 4 so the pool-exhausted / circuit-open
/// semantics are unit-testable without a database).
///
/// A provider failing because its whole key pool is exhausted or its breaker is open is
/// an *expected* degradation — the chain falls through to the next source — so it marks
/// the stage <c>PARTIAL_WARNING</c>, never <c>FAILED</c> (which would sink the whole job).
/// </summary>
internal static class DailyCloseChain
{
    public const string Success = "SUCCESS";
    public const string PartialWarning = "PARTIAL_WARNING";
    public const string Failed = "FAILED";

    /// <summary>Error codes that mean "this source is unavailable by design, fallback ran".</summary>
    public static readonly IReadOnlySet<string> SoftFailureCodes = new HashSet<string>(
        StringComparer.Ordinal
    )
    {
        "Provider.PoolExhausted",
        "Provider.CircuitOpen",
    };

    /// <summary>
    /// Stage status: hard error with zero coverage = FAILED; soft-only error (pool
    /// exhausted / breaker open) with zero coverage = PARTIAL_WARNING; anything else
    /// degrades to PARTIAL_WARNING whenever coverage is incomplete or an error occurred.
    /// </summary>
    public static string StatusFor(
        string? firstErrorCode,
        bool anyHardFailure,
        int touched,
        int expected
    )
    {
        if (firstErrorCode is null && !anyHardFailure)
        {
            return touched < expected ? PartialWarning : Success;
        }

        if (touched == 0)
        {
            return !anyHardFailure && SoftFailureCodes.Contains(firstErrorCode!)
                ? PartialWarning
                : Failed;
        }

        return PartialWarning;
    }

    /// <summary>Overall run status: one hard-failed stage sinks the job; otherwise any
    /// warning degrades to PARTIAL_WARNING.</summary>
    public static string Overall(IReadOnlyList<string> stageStatuses)
    {
        if (stageStatuses.Any(s => s == Failed))
        {
            return Failed;
        }

        return stageStatuses.All(s => s == Success) ? Success : PartialWarning;
    }

    /// <summary>Dividend-queue spacing budget (ms) — default keeps the Brapi free plan
    /// under 10 req/min/key (7000 ms).</summary>
    public static int ResolveDividendSpacingMs(IConfiguration configuration)
    {
        if (
            int.TryParse(configuration["Providers:Brapi:DividendSpacingMs"], out var parsedSpacing)
            && parsedSpacing >= 0
        )
        {
            return parsedSpacing;
        }

        return 7000;
    }
}
