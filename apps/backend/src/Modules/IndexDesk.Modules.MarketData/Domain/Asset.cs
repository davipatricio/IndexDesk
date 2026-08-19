namespace IndexDesk.Modules.MarketData.Domain;

public enum AssetClass
{
    Equity = 0,
    FixedIncome = 1,
    Crypto = 2,
    Commodity = 3,
    RealEstate = 4,
    MultiAsset = 5,
}

public enum AssetCategory
{
    Etf = 0,
    Bdr = 1,
    Stock = 2,
    Index = 3,
    Fii = 4,
}

public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
    public string Administrator { get; set; } = string.Empty;
    public AssetCategory Category { get; set; } = AssetCategory.Etf;
    public AssetClass AssetClass { get; set; } = AssetClass.Equity;
    public string BenchmarkTicker { get; set; } = "IBOV";
    public decimal ManagementFee { get; set; }
    public decimal PerformanceFee { get; set; }
    public decimal TotalNetAssets { get; set; }
    public long NumberOfShareholders { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime InceptionDate { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record QuoteItem(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    long Volume
);

public sealed record AssetDto(
    string Ticker,
    string Name,
    string Manager,
    string Category,
    string AssetClass,
    string Benchmark,
    decimal ManagementFee,
    decimal NetAssets,
    long Shareholders,
    decimal LastPrice,
    decimal ChangeDayPercent,
    decimal ChangeYtdPercent
);
