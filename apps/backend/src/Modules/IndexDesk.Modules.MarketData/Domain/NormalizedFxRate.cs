namespace IndexDesk.Modules.MarketData.Domain;

/// <summary>
/// Normalized FX snapshot (AwesomeAPI contract): no OHLCV, no volume — bid/ask only,
/// with <c>Bid</c> acting as the daily close proxy for backtest conversions.
/// </summary>
public readonly record struct NormalizedFxRate(
    string Pair, // 'USD-BRL', 'EUR-BRL', 'BTC-BRL'
    DateOnly Date,
    decimal Bid,
    decimal Ask,
    decimal Open,
    decimal High,
    decimal Low,
    long TimestampEpochSeconds,
    string SourceProvider
);
