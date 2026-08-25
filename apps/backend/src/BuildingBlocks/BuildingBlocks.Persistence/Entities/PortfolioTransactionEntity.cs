namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Immutable-by-intent transaction log for a portfolio. Editing is logical: a new row references
/// the superseded one via <see cref="AmendedTransactionId"/> and projections only use the latest
/// active version (product decision 2026-08-25, resolves the roadmap immutability tension).
/// </summary>
public class PortfolioTransactionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PortfolioId { get; set; }

    /// <summary>Null when the position is a synthetic index (CDI/Selic cash).</summary>
    public Guid? AssetId { get; set; }

    /// <summary>'CDI' | 'SELIC' — required when <see cref="AssetId"/> is null.</summary>
    public string? SyntheticIndexCode { get; set; }

    /// <summary>BUY | SELL | INCOME | CORP_ACTION | TRANSFER_IN | TRANSFER_OUT</summary>
    public string Type { get; set; } = "BUY";

    /// <summary>Curated broker name or free text ("Outra").</summary>
    public string Broker { get; set; } = string.Empty;

    /// <summary>Free decimals (0.5 BTC). Null for INCOME.</summary>
    public decimal? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    /// <summary>Total gross amount in <see cref="Currency"/> (BRL after fx conversion).</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Single fees field added to cost basis on BUY (product decision).</summary>
    public decimal Fees { get; set; }

    /// <summary>Frozen fx rate from fx_rates at trade date, when currency != BRL.</summary>
    public decimal? FxRate { get; set; }

    public string Currency { get; set; } = "BRL";

    /// <summary>Retroactive unlimited (product decision).</summary>
    public DateOnly TradeDate { get; set; }

    public DateOnly? MaturityDate { get; set; }

    /// <summary>{kind:'split'|'inpc'|'bonificacao'|'subscricao', factor:2} etc.</summary>
    public string? CorpActionJson { get; set; }

    public string? Notes { get; set; }

    /// <summary>True only on amendment rows that supersede an earlier transaction.</summary>
    public bool IsAmendment { get; set; }

    public Guid? AmendedTransactionId { get; set; }
    public Guid? ReversedByTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PortfolioEntity Portfolio { get; set; } = null!;
}
