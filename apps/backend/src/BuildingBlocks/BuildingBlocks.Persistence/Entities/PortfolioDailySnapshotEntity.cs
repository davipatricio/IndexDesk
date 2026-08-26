namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Daily valuation anchor for a portfolio, persisted by <c>PortfolioSnapshotDailyJob</c>.
/// Hypertable in TimescaleDB (see docker/init-db/04-portfolio-snapshots.sql); upsert per
/// (portfolio, date) keeps re-execution idempotent.
/// </summary>
public class PortfolioDailySnapshotEntity
{
    public Guid PortfolioId { get; set; }
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Total portfolio value at the last available local price of the day.</summary>
    public decimal TotalValue { get; set; }

    public decimal InvestedAmount { get; set; }
    public string? AllocationJson { get; set; }
    public decimal? TwrSinceInception { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
