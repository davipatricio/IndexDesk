namespace IndexDesk.Modules.MarketData.Domain;

public readonly record struct NormalizedQuote(
    string Ticker,
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjClose,
    decimal Volume,
    int? TradesCount,
    string SourceProvider,
    DateTimeOffset FetchedAtUtc
);
