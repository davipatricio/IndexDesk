namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Multi-goal tracker attached to a portfolio (M-P5). Exactly one target column applies per row,
/// selected by <see cref="Kind"/>: TARGET_AMOUNT → TargetValue, TARGET_RETURN_PCT → TargetPct,
/// TARGET_DATE → TargetDate. The contribution/rate columns feed the projection calculator.
/// </summary>
public class PortfolioGoalEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PortfolioId { get; set; }

    /// <summary>TARGET_AMOUNT | TARGET_RETURN_PCT | TARGET_DATE</summary>
    public string Kind { get; set; } = "TARGET_AMOUNT";

    /// <summary>BRL target — required when Kind = TARGET_AMOUNT.</summary>
    public decimal? TargetValue { get; set; }

    /// <summary>Total return % target — required when Kind = TARGET_RETURN_PCT.</summary>
    public decimal? TargetPct { get; set; }

    /// <summary>Deadline — required when Kind = TARGET_DATE.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>Optional planned monthly deposit feeding the compound projection.</summary>
    public decimal? MonthlyContribution { get; set; }

    /// <summary>Annual % assumption for projections (e.g. CDI estimate).</summary>
    public decimal? AssumedAnnualRate { get; set; }

    /// <summary>active | achieved | cancelled</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PortfolioEntity Portfolio { get; set; } = null!;
}
