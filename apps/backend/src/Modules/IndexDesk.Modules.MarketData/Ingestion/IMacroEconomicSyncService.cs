using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface IMacroEconomicSyncService
{
    Task<Result<MacroSyncSummary>> SyncAllAsync(
        DateOnly? startDate = null,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct MacroSyncSummary(
    string Status,
    int SeriesSynced,
    int RecordsUpserted,
    long ElapsedMilliseconds,
    IReadOnlyList<string> FailedSeries
);
