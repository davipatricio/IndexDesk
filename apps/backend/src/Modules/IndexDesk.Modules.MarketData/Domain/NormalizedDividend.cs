namespace IndexDesk.Modules.MarketData.Domain;

public readonly record struct NormalizedDividend(
    string Ticker,
    DateOnly ComDate,
    DateOnly? PaymentDate,
    decimal Rate,
    string DividendType, // Rendimento, Dividendo, JCP
    string Currency,
    string SourceProvider
);
