using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Pipeline;

public interface IFallbackMarketDataService
{
    Task<Result<IReadOnlyList<NormalizedQuote>>> GetQuotesWithFallbackAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );

    Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsWithFallbackAsync(
        string ticker,
        CancellationToken cancellationToken = default
    );
}
