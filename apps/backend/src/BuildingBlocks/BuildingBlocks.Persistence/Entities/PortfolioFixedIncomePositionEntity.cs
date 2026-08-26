namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Fixed-income parameters attached to a portfolio position (M-P3): generic indexer model
/// covering CDB/LC/CRI/CRA/debênture/LCI/LCA/Tesouro-like holdings and synthetic CDI/Selic cash.
/// Accrual is computed on demand / by PortfolioAccrualDailyJob from local macro series only.
/// </summary>
public class PortfolioFixedIncomePositionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PortfolioId { get; set; }

    /// <summary>Null when the position targets a synthetic index.</summary>
    public Guid? AssetId { get; set; }

    /// <summary>'CDI' | 'SELIC' — matches portfolio_transactions.synthetic_index_code.</summary>
    public string? SyntheticIndexCode { get; set; }

    /// <summary>CDI_PERCENT | CDI_PLUS | SELIC | IPCA_PLUS | PREFIXED</summary>
    public string Indexer { get; set; } = "CDI_PERCENT";

    /// <summary>% do CDI (ex.: 95) | spread a.a. % | taxa pré a.a. %.</summary>
    public decimal IndexerRate { get; set; }

    public decimal Principal { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public string Liquidity { get; set; } = "maturity";
    public string TaxRegime { get; set; } = "regressive";

    /// <summary>Último valor corrigido auditável (idempotência do job).</summary>
    public decimal AccruedValue { get; set; }
    public DateOnly? LastAccrualDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
