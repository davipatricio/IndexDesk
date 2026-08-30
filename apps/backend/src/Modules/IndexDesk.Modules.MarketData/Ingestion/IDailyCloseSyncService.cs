using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface IDailyCloseSyncService
{
    /// <summary>
    /// Post-close daily flow (22:00 UTC MON-FRI): ONE Brapi batch call for the whole
    /// universe, then per-ticker gaps fall back to the Yahoo sidecar and finally to
    /// the TradingView sidecar. Brapi budget rules respected: single batch call per
    /// trading day, dividend queue spaced ≥ 7 s, never bulk history.
    /// </summary>
    /// <param name="targetTickers">Optional allow-list. Null/empty = resolved from
    /// <see cref="SyncUniverse"/> (config or full active universe).</param>
    /// <param name="lookbackDays">Window length when fetching each ticker from the
    /// sidecar chain (used as the "have we already covered this date?" hint as well).</param>
    /// <param name="targetDate">Date the run is being executed for. Defaults to UTC
    /// today. Catch-up bootstrap sets this to a specific past business day so the
    /// Brapi batch (which only exposes "today" data) is automatically skipped and
    /// the chain falls through to Yahoo for historical coverage.</param>
    Task<Result<DailyCloseSummary>> SyncDailyCloseAsync(
        IReadOnlyList<string>? targetTickers = null,
        int lookbackDays = 5,
        DateOnly? targetDate = null,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct StageSummary(
    string Stage,
    string Provider,
    string Status,
    int QuotesUpserted,
    int TickersTouched,
    string? Error
);

public readonly record struct DailyCloseSummary(
    string Status,
    DateOnly TradeDate,
    int UniverseSize,
    long ElapsedMilliseconds,
    IReadOnlyList<StageSummary> Stages
);
