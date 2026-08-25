namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// User portfolio (carteira). Private by default; may be public or link-restricted.
/// Currency is BRL-only for now (product decision), foreign exposure converts via fx_rates.
/// </summary>
public class PortfolioEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Pure label (conservador/moderado/arrojado) — no allocation validation.</summary>
    public string RiskProfile { get; set; } = "moderado";

    /// <summary>private | public | link</summary>
    public string Visibility { get; set; } = "private";

    /// <summary>percent_only | full_values — what public viewers can see.</summary>
    public string PublicValuesMode { get; set; } = "percent_only";

    /// <summary>Null = anonymous "Investidor X" on public pages.</summary>
    public string? DisplayIdentity { get; set; }

    /// <summary>Public slug (/c/[slug]); null while private.</summary>
    public string? Slug { get; set; }

    public string? ShareTokenHash { get; set; }
    public DateTimeOffset? ShareExpiresAt { get; set; }

    /// <summary>Editable target allocation per asset class, e.g. {"ETF":40,"RF":30}.</summary>
    public string? TargetAllocationJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PortfolioTransactionEntity> Transactions { get; set; } =
        new List<PortfolioTransactionEntity>();
}
