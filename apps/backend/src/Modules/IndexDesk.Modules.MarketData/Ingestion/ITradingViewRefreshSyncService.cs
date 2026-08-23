using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Ingestion;

public interface ITradingViewRefreshSyncService
{
    /// <summary>
    /// Refreshes OHLCV through the TradingView sidecar for tickers the other chain
    /// members do not cover (benchmarks/global symbols) or whose local series went
    /// stale. Thin shell over the module clients; writes sync_job_logs.
    /// </summary>
    Task<Result<TvRefreshSummary>> RefreshAsync(
        IReadOnlyList<string>? targetTickers = null,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct TvRefreshSummary(
    string Status,
    int TickersRequested,
    int TickersRefreshed,
    int QuotesUpserted,
    long ElapsedMilliseconds,
    IReadOnlyList<string> FailedTickers
);
