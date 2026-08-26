using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

public interface IPortfolioService
{
    Task<Result<PortfolioDto>> CreateAsync(
        Guid userId,
        CreatePortfolioRequest request,
        CancellationToken ct
    );
    Task<Result<IReadOnlyList<PortfolioDto>>> ListAsync(Guid userId, CancellationToken ct);
    Task<Result<PortfolioSummaryDto>> GetSummaryAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    );
    Task<Result<PortfolioSummaryDto>> GetSummaryInternalAsync(
        Guid portfolioId,
        CancellationToken ct
    );
    Task<Result<PortfolioDto>> UpdateAsync(
        Guid userId,
        Guid portfolioId,
        UpdatePortfolioRequest request,
        CancellationToken ct
    );
    Task<Result> DeleteAsync(Guid userId, Guid portfolioId, CancellationToken ct);
}
