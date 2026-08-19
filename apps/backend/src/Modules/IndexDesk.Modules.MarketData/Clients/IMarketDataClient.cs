using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Clients;

public interface IMarketDataClient
{
    string ProviderName { get; }
    int Priority { get; } // 1 = Primary (Brapi), 2 = Secondary (Yahoo), 3 = Contingency (HG Brasil)

    bool SupportsTicker(string ticker);

    Task<Result<IReadOnlyList<NormalizedQuote>>> GetHistoricalQuotesAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );

    Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsAsync(
        string ticker,
        CancellationToken cancellationToken = default
    );
}
