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
    Task<Result<DailyCloseSummary>> SyncDailyCloseAsync(
        IReadOnlyList<string>? targetTickers = null,
        int lookbackDays = 5,
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
