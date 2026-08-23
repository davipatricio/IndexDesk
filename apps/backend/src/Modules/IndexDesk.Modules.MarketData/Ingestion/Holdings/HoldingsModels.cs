namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>One parsed holding line before asset resolution/persistence.</summary>
public readonly record struct ParsedHolding(
    string? Ticker,
    string Name,
    decimal WeightPercentage,
    string? Sector,
    string? Country
);

/// <summary>Raised when a feed layout no longer matches expectations.</summary>
public sealed class HoldingsLayoutException : Exception
{
    public HoldingsLayoutException(string message)
        : base(message) { }
}
