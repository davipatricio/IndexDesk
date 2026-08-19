namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class AssetDividendEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public DateOnly ComDate { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public decimal Rate { get; set; }
    public string DividendType { get; set; } = "Rendimento"; // Rendimento, Dividendo, JCP
    public string Currency { get; set; } = "BRL";
    public string SourceProvider { get; set; } = "BRAPI";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AssetEntity Asset { get; set; } = null!;
}
