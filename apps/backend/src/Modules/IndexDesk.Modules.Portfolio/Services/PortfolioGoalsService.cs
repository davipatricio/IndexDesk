using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Multi-goal CRUD over portfolio_goals. Progress % is computed against the currentValue the
/// caller brings (no revaluation here); month projections delegate to the pure
/// GoalProjectionCalculator (CompoundMonths when contribution/rate are set, run-rate otherwise).
/// Cross-user access returns NotFound, never 403.
/// </summary>
public sealed class PortfolioGoalsService(IndexDeskDbContext db) : IPortfolioGoalsService
{
    private static readonly string[] ValidKinds =
    [
        "TARGET_AMOUNT",
        "TARGET_RETURN_PCT",
        "TARGET_DATE",
    ];

    private static readonly string[] ValidStatuses = ["active", "achieved", "cancelled"];

    // DbSet de PortfolioGoalEntity entra quando o DbContext for mapeado; Set<T>() já funciona hoje.
    private IQueryable<PortfolioGoalEntity> Goals => db.Set<PortfolioGoalEntity>();

    public async Task<Result<GoalDto>> CreateAsync(
        Guid userId,
        Guid portfolioId,
        CreateGoalRequest request,
        CancellationToken ct
    )
    {
        var owns = await OwnsAsync(userId, portfolioId, ct);
        if (owns is null)
            return Result<GoalDto>.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        var kind = NormalizeKind(request.Kind);
        var validation = ValidateKindTargets(
            kind,
            request.TargetValue,
            request.TargetPct,
            request.TargetDate
        );
        if (validation is not null)
            return Result<GoalDto>.Failure(validation);

        var goal = new PortfolioGoalEntity
        {
            PortfolioId = portfolioId,
            Kind = kind,
            TargetValue = request.TargetValue,
            TargetPct = request.TargetPct,
            TargetDate = request.TargetDate,
            MonthlyContribution = request.MonthlyContribution,
            AssumedAnnualRate = request.AssumedAnnualRate,
        };
        db.Add(goal);
        await db.SaveChangesAsync(ct);
        return Result<GoalDto>.Success(ToDto(goal));
    }

    public async Task<Result<GoalDto>> UpdateAsync(
        Guid userId,
        Guid portfolioId,
        Guid goalId,
        UpdateGoalRequest request,
        CancellationToken ct
    )
    {
        var owns = await OwnsAsync(userId, portfolioId, ct);
        if (owns is null)
            return Result<GoalDto>.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        var goal = await Goals.FirstOrDefaultAsync(
            g => g.Id == goalId && g.PortfolioId == portfolioId,
            ct
        );
        if (goal is null)
            return Result<GoalDto>.Failure(new Error("Goal.NotFound", "Meta não encontrada."));

        var kind = NormalizeKind(request.Kind ?? goal.Kind);
        var targetValue = request.TargetValue ?? goal.TargetValue;
        var targetPct = request.TargetPct ?? goal.TargetPct;
        var targetDate = request.TargetDate ?? goal.TargetDate;

        if (!ValidKinds.Contains(kind))
            return Result<GoalDto>.Failure(InvalidKind());
        var validation = ValidateKindTargets(kind, targetValue, targetPct, targetDate);
        if (validation is not null)
            return Result<GoalDto>.Failure(validation);

        if (request.Status is not null)
        {
            var status = request.Status.Trim().ToLowerInvariant();
            if (!ValidStatuses.Contains(status))
                return Result<GoalDto>.Failure(
                    Error.Validation(
                        "GoalStatusInvalid",
                        "Status inválido. Use active, achieved ou cancelled."
                    )
                );
            goal.Status = status;
        }

        goal.Kind = kind;
        goal.TargetValue = targetValue;
        goal.TargetPct = targetPct;
        goal.TargetDate = targetDate;
        goal.MonthlyContribution = request.MonthlyContribution ?? goal.MonthlyContribution;
        goal.AssumedAnnualRate = request.AssumedAnnualRate ?? goal.AssumedAnnualRate;

        await db.SaveChangesAsync(ct);
        return Result<GoalDto>.Success(ToDto(goal));
    }

    public async Task<Result> DeleteAsync(
        Guid userId,
        Guid portfolioId,
        Guid goalId,
        CancellationToken ct
    )
    {
        var owns = await OwnsAsync(userId, portfolioId, ct);
        if (owns is null)
            return Result.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        var goal = await Goals.FirstOrDefaultAsync(
            g => g.Id == goalId && g.PortfolioId == portfolioId,
            ct
        );
        if (goal is null)
            return Result.Failure(new Error("Goal.NotFound", "Meta não encontrada."));

        db.Remove(goal);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GoalProgressDto>>> ListAsync(
        Guid userId,
        Guid portfolioId,
        decimal currentValue,
        decimal? currentReturnPct,
        CancellationToken ct
    )
    {
        var owns = await OwnsAsync(userId, portfolioId, ct);
        if (owns is null)
            return Result<IReadOnlyList<GoalProgressDto>>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var goals = await Goals
            .Where(g => g.PortfolioId == portfolioId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(ct);

        var items = goals
            .Select(g => new GoalProgressDto(
                ToDto(g),
                currentValue,
                ProgressPercent(g, currentValue, currentReturnPct)
            ))
            .ToList();

        return Result<IReadOnlyList<GoalProgressDto>>.Success(items);
    }

    public async Task<Result<GoalProjectionDto>> ProjectionAsync(
        Guid userId,
        Guid portfolioId,
        Guid goalId,
        decimal currentValue,
        decimal? recentMonthlyReturnPct,
        CancellationToken ct
    )
    {
        var owns = await OwnsAsync(userId, portfolioId, ct);
        if (owns is null)
            return Result<GoalProjectionDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var goal = await Goals.FirstOrDefaultAsync(
            g => g.Id == goalId && g.PortfolioId == portfolioId,
            ct
        );
        if (goal is null)
            return Result<GoalProjectionDto>.Failure(
                new Error("Goal.NotFound", "Meta não encontrada.")
            );

        int? months = goal.Kind switch
        {
            // Com aporte/juros assumidos → simulação mês a mês.
            "TARGET_AMOUNT" when goal.MonthlyContribution is > 0 || goal.AssumedAnnualRate is > 0 =>
                GoalProjectionCalculator.CompoundMonths(
                    currentValue,
                    goal.MonthlyContribution ?? 0m,
                    goal.AssumedAnnualRate ?? 0m,
                    goal.TargetValue!.Value
                ),
            // Sem premissas → run-rate do retorno recente informado pelo caller.
            "TARGET_AMOUNT" => GoalProjectionCalculator.RunRateMonths(
                currentValue,
                goal.TargetValue!.Value,
                recentMonthlyReturnPct ?? 0m
            ),
            _ => null, // RETURN_PCT/DATE não projetam meses hoje
        };

        var estimatedDate = months.HasValue
            ? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(months.Value)
            : (DateOnly?)null;

        return Result<GoalProjectionDto>.Success(
            new GoalProjectionDto(goal.Id, goal.Kind, currentValue, months, estimatedDate)
        );
    }

    // ---------- helpers ----------

    private static string NormalizeKind(string? kind) =>
        (kind ?? string.Empty).Trim().ToUpperInvariant();

    private static Error InvalidKind() =>
        Error.Validation(
            "GoalKindInvalid",
            "Kind inválido. Use TARGET_AMOUNT, TARGET_RETURN_PCT ou TARGET_DATE."
        );

    /// <summary>Cada kind exige a coluna-alvo correspondente, positiva quando numérica.</summary>
    private static Error? ValidateKindTargets(
        string kind,
        decimal? targetValue,
        decimal? targetPct,
        DateOnly? targetDate
    ) =>
        kind switch
        {
            "TARGET_AMOUNT" when targetValue is not > 0 => Error.Validation(
                "GoalTargetRequired",
                "Meta de valor requer TargetValue positivo."
            ),
            "TARGET_RETURN_PCT" when targetPct is not > 0 => Error.Validation(
                "GoalTargetRequired",
                "Meta de rentabilidade requer TargetPct positivo."
            ),
            "TARGET_DATE" when targetDate is null => Error.Validation(
                "GoalTargetRequired",
                "Meta de data requer TargetDate."
            ),
            _ when !ValidKinds.Contains(kind) => InvalidKind(),
            _ => null,
        };

    /// <summary>
    /// Progresso % por kind: valor → parte/alvo; retorno → atual/alvo; data → tempo decorrido.
    /// Cancelled = null; achieved = 100. Sempre limitado a [0, 100].
    /// </summary>
    private static decimal? ProgressPercent(
        PortfolioGoalEntity goal,
        decimal currentValue,
        decimal? currentReturnPct
    )
    {
        if (goal.Status == "cancelled")
            return null;
        if (goal.Status == "achieved")
            return 100m;

        return goal.Kind switch
        {
            "TARGET_AMOUNT" when goal.TargetValue is > 0 => Math.Min(
                100m,
                decimal.Round(currentValue / goal.TargetValue.Value * 100m, 2)
            ),
            "TARGET_RETURN_PCT" when goal.TargetPct is > 0 && currentReturnPct.HasValue => Math.Min(
                100m,
                decimal.Round(currentReturnPct.Value / goal.TargetPct.Value * 100m, 2)
            ),
            "TARGET_DATE" when goal.TargetDate.HasValue => DateProgress(goal),
            _ => null,
        };
    }

    private static decimal DateProgress(PortfolioGoalEntity goal)
    {
        var start = DateOnly.FromDateTime(goal.CreatedAt);
        var end = goal.TargetDate!.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalDays = end.DayNumber - start.DayNumber;
        if (totalDays <= 0)
            return 100m;

        var elapsed = Math.Clamp(today.DayNumber - start.DayNumber, 0, totalDays);
        return decimal.Round(elapsed * 100m / totalDays, 2);
    }

    private async Task<PortfolioEntity?> OwnsAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    ) =>
        await db.Portfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.UserId == userId, ct);

    private static GoalDto ToDto(PortfolioGoalEntity g) =>
        new(
            g.Id,
            g.PortfolioId,
            g.Kind,
            g.TargetValue,
            g.TargetPct,
            g.TargetDate,
            g.MonthlyContribution,
            g.AssumedAnnualRate,
            g.Status,
            g.CreatedAt
        );
}
