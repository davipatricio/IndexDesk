using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

public sealed record PerformancePointDto(DateOnly Date, decimal Value, decimal ExternalFlow);

public sealed record BenchmarkSeriesDto(string Code, IReadOnlyList<decimal> NormalizedValues);

public sealed record PerformanceResultDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PerformancePointDto> Series,
    decimal TotalReturnPercent,
    decimal TwrPercentPeriod,
    decimal? MwrPercentAnnualized,
    decimal VolatilityPercentAnnualized,
    decimal SharpeRatio,
    decimal MaxDrawdownPercent,
    IReadOnlyList<BenchmarkSeriesDto> Benchmarks
);

public interface IPortfolioPerformanceService
{
    Task<Result<PerformanceResultDto>> GetAsync(
        Guid userId,
        Guid portfolioId,
        DateOnly? from,
        DateOnly? to,
        string? benchmarks,
        CancellationToken ct
    );

    /// <summary>Latest valuation for the daily snapshot job (upsert target).</summary>
    Task<int> SnapshotAllPortfoliosAsync(CancellationToken ct);
}
