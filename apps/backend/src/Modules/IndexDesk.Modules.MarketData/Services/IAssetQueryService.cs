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

    /// <summary>
    /// Ranks active assets by a performance metric (e.g. sharpe, retorno12m, volume).
    /// Assets without data for the metric are placed last regardless of direction.
    /// </summary>
    Task<PagedResult<AssetRankingDto>> GetRankingsAsync(
        string? assetType,
        string metric,
        string? orderDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    /// <summary>Latest CDI/Selic/IPCA snapshot with trailing 12m accumulation.</summary>
    Task<IReadOnlyList<MarketIndicatorDto>> GetMarketIndicatorsAsync(
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
