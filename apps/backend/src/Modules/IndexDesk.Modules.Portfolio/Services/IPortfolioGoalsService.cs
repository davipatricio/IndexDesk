using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

// ---------- Requests ----------

public sealed record CreateGoalRequest(
    string Kind,
    decimal? TargetValue = null,
    decimal? TargetPct = null,
    DateOnly? TargetDate = null,
    decimal? MonthlyContribution = null,
    decimal? AssumedAnnualRate = null
);

public sealed record UpdateGoalRequest(
    string? Kind = null,
    decimal? TargetValue = null,
    decimal? TargetPct = null,
    DateOnly? TargetDate = null,
    decimal? MonthlyContribution = null,
    decimal? AssumedAnnualRate = null,
    string? Status = null
);

// ---------- Responses ----------

public sealed record GoalDto(
    Guid Id,
    Guid PortfolioId,
    string Kind,
    decimal? TargetValue,
    decimal? TargetPct,
    DateOnly? TargetDate,
    decimal? MonthlyContribution,
    decimal? AssumedAnnualRate,
    string Status,
    DateTime CreatedAt
);

/// <summary>Meta com o progresso % calculado contra o valor atual informado pelo caller.</summary>
public sealed record GoalProgressDto(GoalDto Goal, decimal CurrentValue, decimal? ProgressPercent);

public sealed record GoalProjectionDto(
    Guid GoalId,
    string Kind,
    decimal CurrentValue,
    int? EstimatedMonths,
    DateOnly? EstimatedDate
);

/// <summary>
/// Multi-goal CRUD sobre portfolio_goals + projeção via GoalProjectionCalculator. O progresso %
/// é calculado contra o currentValue informado pelo caller (este serviço não reavalia posições).
/// Cross-user access returns NotFound, never 403.
/// </summary>
public interface IPortfolioGoalsService
{
    Task<Result<GoalDto>> CreateAsync(
        Guid userId,
        Guid portfolioId,
        CreateGoalRequest request,
        CancellationToken ct
    );
    Task<Result<GoalDto>> UpdateAsync(
        Guid userId,
        Guid portfolioId,
        Guid goalId,
        UpdateGoalRequest request,
        CancellationToken ct
    );
    Task<Result> DeleteAsync(Guid userId, Guid portfolioId, Guid goalId, CancellationToken ct);

    /// <summary>
    /// Lista as metas da carteira com progresso %. currentReturnPct alimenta metas
    /// TARGET_RETURN_PCT (sem ele, o progresso desse kind vem null).
    /// </summary>
    Task<Result<IReadOnlyList<GoalProgressDto>>> ListAsync(
        Guid userId,
        Guid portfolioId,
        decimal currentValue,
        decimal? currentReturnPct,
        CancellationToken ct
    );

    /// <summary>
    /// Projeção em meses: com aporte/juros assumidos usa CompoundMonths; caso contrário
    /// RunRateMonths com recentMonthlyReturnPct informado pelo caller.
    /// </summary>
    Task<Result<GoalProjectionDto>> ProjectionAsync(
        Guid userId,
        Guid portfolioId,
        Guid goalId,
        decimal currentValue,
        decimal? recentMonthlyReturnPct,
        CancellationToken ct
    );
}
