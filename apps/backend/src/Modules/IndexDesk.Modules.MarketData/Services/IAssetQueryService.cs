using IndexDesk.BuildingBlocks.Common.Pagination;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Services;

public interface IAssetQueryService
{
    Task<PagedResult<AssetSummaryDto>> ListAsync(
        string? search,
        string? assetType,
        string? currency,
        string? orderBy,
        string? orderDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task<AssetDetailDto?> GetDetailAsync(
        string ticker,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<QuoteSeriesDto>> GetQuotesAsync(
        string ticker,
        DateOnly? from,
        DateOnly? to,
        int? days,
        CancellationToken cancellationToken = default
    );

    Task<PerformanceResponseDto?> GetPerformanceAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        string returnType,
        bool includeBenchmarks,
        CancellationToken cancellationToken = default
    );
}
