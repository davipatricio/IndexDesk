using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

public sealed record FixedIncomeParamDto(
    Guid Id,
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Indexer,
    decimal IndexerRate,
    decimal Principal,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string Liquidity,
    string TaxRegime,
    decimal AccruedValue,
    DateOnly? LastAccrualDate
);

public sealed record AttachFixedIncomeRequest(
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Indexer,
    decimal IndexerRate,
    decimal Principal,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string Liquidity = "maturity",
    string TaxRegime = "regressive"
);

public sealed record TimelineItemDto(DateOnly Date, string Label, string Kind, decimal Amount);

public interface IPortfolioFixedIncomeService
{
    /// <summary>Anexa/atualiza parâmetros RF para uma posição da carteira.</summary>
    Task<Result<FixedIncomeParamDto>> AttachAsync(
        Guid userId,
        Guid portfolioId,
        AttachFixedIncomeRequest request,
        CancellationToken ct
    );

    Task<Result<IReadOnlyList<FixedIncomeParamDto>>> ListAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    );

    Task<Result> DetachAsync(
        Guid userId,
        Guid portfolioId,
        Guid fixedIncomeId,
        CancellationToken ct
    );

    /// <summary>Vencimentos e carências próximos (transações + parâmetros).</summary>
    Task<Result<IReadOnlyList<TimelineItemDto>>> GetTimelineAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    );

    /// <summary>Worker: recalcula o accrual de todos os parâmetros (upsert idempotente por dia).</summary>
    Task<int> AccrueAllAsync(CancellationToken ct);
}
