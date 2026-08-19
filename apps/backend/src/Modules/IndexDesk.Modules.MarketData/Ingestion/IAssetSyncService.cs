using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface IAssetSyncService
{
    Task<Result<SyncExecutionSummary>> SyncDailyQuotesAsync(
        IReadOnlyList<string>? targetTickers = null,
        int lookbackDays = 5,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct SyncExecutionSummary(
    string Status,
    int AssetsSynced,
    int QuotesIngested,
    int DividendsIngested,
    long ElapsedMilliseconds,
    IReadOnlyList<string> FailedTickers
);
