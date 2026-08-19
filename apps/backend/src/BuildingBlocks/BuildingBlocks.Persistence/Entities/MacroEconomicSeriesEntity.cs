namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class MacroEconomicSeriesEntity
{
    public int SeriesCode { get; set; } // 12 = CDI, 11 = Selic, 433 = IPCA, 189 = IGP-M
    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
