namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// One line of an ETF portfolio on a given date (MODELS.md §etf_holdings).
/// Populated weekly by <c>HoldingsWeeklySyncJob</c> from manager feeds
/// (iShares CSV, SPDR XLSX, It Now/Investo HTML). Deduplication key:
/// (EtfAssetId, AsOfDate, HoldingTicker).
/// </summary>
public class EtfHoldingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EtfAssetId { get; set; }
    public string? HoldingTicker { get; set; }
    public string HoldingName { get; set; } = string.Empty;
    public decimal WeightPercentage { get; set; } // 0.0850 = 8.50%
    public string? Sector { get; set; }
    public string? Country { get; set; }
    public DateOnly AsOfDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AssetEntity? Asset { get; set; }
}
