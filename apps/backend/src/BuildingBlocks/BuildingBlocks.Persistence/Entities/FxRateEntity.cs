namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Daily FX close for a pair (USD-BRL, EUR-BRL, BTC-BRL) persisted by
/// <c>FxRatesDailySyncJob</c>. FX contract has no OHLCV/volume: bid doubles as the
/// close proxy and ask completes the spread snapshot.
/// </summary>
public class FxRateEntity
{
    public string Pair { get; set; } = string.Empty; // 'USD-BRL', 'EUR-BRL', 'BTC-BRL'
    public DateOnly Date { get; set; }
    public decimal Bid { get; set; } // close proxy
    public decimal Ask { get; set; }
    public string SourceProvider { get; set; } = "AwesomeApi";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
