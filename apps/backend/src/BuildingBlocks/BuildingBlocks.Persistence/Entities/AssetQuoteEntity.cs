namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class AssetQuoteEntity
{
    public Guid AssetId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal AdjClose { get; set; }
    public decimal Volume { get; set; }
    public int? TradesCount { get; set; }
    public string SourceProvider { get; set; } = "BRAPI";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AssetEntity Asset { get; set; } = null!;
}
