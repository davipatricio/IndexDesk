namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class AssetEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "ETF"; // ETF, BDR_ETF, FII, STOCK, INDEX
    public string? Cnpj { get; set; }
    public string? Isin { get; set; }
    public string Currency { get; set; } = "BRL";
    public string? TradingViewSymbol { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsManuallyOverridden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AssetQuoteEntity> Quotes { get; set; } = new List<AssetQuoteEntity>();
    public ICollection<AssetDividendEntity> Dividends { get; set; } =
        new List<AssetDividendEntity>();
}
