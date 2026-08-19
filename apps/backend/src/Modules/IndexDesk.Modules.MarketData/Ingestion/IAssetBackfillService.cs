using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface IAssetBackfillService
{
    Task<Result<BackfillExecutionSummary>> BackfillAssetAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );

    Task<Result<IReadOnlyList<BackfillExecutionSummary>>> BackfillPilotAssetsAsync(
        DateOnly? defaultStartDate = null,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct BackfillExecutionSummary(
    string Ticker,
    string Status,
    int QuotesIngested,
    int DividendsIngested,
    string SourceProvider,
    long ElapsedMilliseconds,
    string? ErrorMessage
);
