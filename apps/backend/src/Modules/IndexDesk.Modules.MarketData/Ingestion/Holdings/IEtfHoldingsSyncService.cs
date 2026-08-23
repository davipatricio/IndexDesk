using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

public interface IEtfHoldingsSyncService
{
    /// <summary>
    /// Weekly manager-feed ingestion (SAT 08:00 UTC): iShares CSV, SPDR XLSX and the
    /// It Now/Investo composition pages, upserted idempotently into etf_holdings
    /// (dedupe on etf_asset_id + as_of_date + holding_ticker). A failing source is a
    /// PARTIAL_WARNING — the job always continues with the other sources.
    /// </summary>
    Task<Result<HoldingsSyncSummary>> SyncWeeklyAsync(
        CancellationToken cancellationToken = default
    );
}

public readonly record struct HoldingsSourceSummary(
    string Source,
    string Ticker,
    string Status,
    int HoldingsUpserted,
    DateOnly AsOfDate,
    string? Error
);

public readonly record struct HoldingsSyncSummary(
    string Status,
    int SourcesAttempted,
    int SourcesSucceeded,
    int TotalRowsUpserted,
    long ElapsedMilliseconds,
    IReadOnlyList<HoldingsSourceSummary> Sources
);
