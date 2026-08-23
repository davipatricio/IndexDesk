using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface IFxRateSyncService
{
    /// <summary>Post-close snapshot for the default pairs (USD-BRL, EUR-BRL, BTC-BRL):
    /// one AwesomeAPI /last call, bid persisted as close proxy. Idempotent per (pair, date).</summary>
    Task<Result<FxSyncSummary>> SyncLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>Daily history backfill for a single pair via /json/daily.</summary>
    Task<Result<FxSyncSummary>> SyncRangeAsync(
        string pair,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct FxSyncSummary(
    string Status,
    int PairsRequested,
    int PairsPersisted,
    int RecordsUpserted,
    long ElapsedMilliseconds,
    string? Error
);
