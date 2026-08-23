using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

internal sealed record DividendQueueOutcome(
    int Processed,
    string? FirstErrorCode,
    string? FirstErrorMessage,
    bool AnyHardFailure
)
{
    public bool AnyFailure => FirstErrorCode is not null || AnyHardFailure;
}

/// <summary>
/// Pure orchestration of the spaced Brapi dividend queue (budget rule: ≥7 s between
/// requests keeps the free plan under 10 req/min/key). One delay follows EVERY ticker —
/// including the last — which matches the historical behavior of the inline loop.
///
/// Extracted from <see cref="DailyCloseSyncService"/> in Fase 4 so the spacing and the
/// soft/hard failure classification are unit-testable without a database: callers inject
/// the per-ticker processing and the delay delegate.
/// </summary>
internal static class DividendQueue
{
    public static async Task<DividendQueueOutcome> RunAsync(
        IReadOnlyList<string> tickers,
        Func<string, CancellationToken, Task<Result<int>>> processTicker,
        Func<int, Task> delayAsync,
        int spacingMs,
        CancellationToken cancellationToken
    )
    {
        var processed = 0;
        string? firstErrorCode = null;
        string? firstErrorMessage = null;
        var anyHardFailure = false;

        foreach (var ticker in tickers)
        {
            try
            {
                var result = await processTicker(ticker, cancellationToken);
                if (result.IsSuccess)
                {
                    processed++;
                }
                else
                {
                    // Soft failures (pool exhausted / breaker open) never escalate the
                    // stage past PARTIAL_WARNING — see DailyCloseChain.StatusFor.
                    if (firstErrorCode is null)
                    {
                        firstErrorCode = result.Error.Code;
                        firstErrorMessage = result.Error.Message;
                    }

                    if (!DailyCloseChain.SoftFailureCodes.Contains(result.Error.Code))
                    {
                        anyHardFailure = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (firstErrorCode is null)
                {
                    firstErrorCode = "DailyClose.Exception";
                    firstErrorMessage = ex.Message;
                }

                anyHardFailure = true;
            }

            await delayAsync(spacingMs);
        }

        return new DividendQueueOutcome(
            processed,
            firstErrorCode,
            firstErrorMessage,
            anyHardFailure
        );
    }
}
