namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Curated catalog of market index benchmarks that can be ingested as quote series.
/// Entries here are the only tickers the backfill pipeline may auto-create as
/// <c>AssetType = "INDEX"</c> — regular B3 assets still require explicit curation.
/// </summary>
public static class BenchmarkCatalog
{
    public sealed record BenchmarkDefinition(
        string Ticker,
        string Name,
        string YahooSymbol,
        DateOnly HistoryStart,
        string? TradingViewSymbol
    );

    public static readonly IReadOnlyDictionary<string, BenchmarkDefinition> All = new Dictionary<
        string,
        BenchmarkDefinition
    >(StringComparer.OrdinalIgnoreCase)
    {
        // Full historical depth available via Yahoo (^BVSP).
        ["IBOV"] = new BenchmarkDefinition(
            "IBOV",
            "Ibovespa",
            "^BVSP",
            new DateOnly(2015, 1, 1),
            "TVC:IBOV"
        ),

        // Yahoo only exposes the current IFIX level (no history), so ingestion is
        // forward-only: the local series grows one session at a time from today.
        ["IFIX"] = new BenchmarkDefinition(
            "IFIX",
            "IFIX (Índice de Fundos Imobiliários)",
            "IFIX.SA",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7),
            null
        ),
    };

    public static bool IsBenchmark(string ticker) =>
        !string.IsNullOrWhiteSpace(ticker) && All.ContainsKey(ticker.Trim().ToUpperInvariant());
}
