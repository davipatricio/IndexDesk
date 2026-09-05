using IndexDesk.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Resolves which tickers the scheduled jobs should ingest: explicit CLI/config list
/// wins; otherwise every active non-INDEX asset in the catalog; otherwise the pilot
/// fallback (same default <see cref="AssetSyncService"/> uses when nothing is curated).
/// </summary>
public static class SyncUniverse
{
    public const string DailyTickersConfigKey = "Providers:Sync:DailyTickers";
    public const string TradingViewTickersConfigKey = "Providers:Sync:TradingViewTickers";
    public const string AwesomeApiPairsConfigKey = "Providers:AwesomeApi:Pairs";

    public static readonly IReadOnlyList<string> PilotFallback = new[]
    {
        "MXRF11",
        "VWRA11",
        "GOLD11",
        "WRLD11",
    };

    public static IReadOnlyList<string> FromList(string? commaSeparated)
    {
        var parsed = (commaSeparated ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .Distinct()
            .ToList();
        return parsed.Count > 0 ? parsed : [];
    }

    /// <summary>Config override, else active catalog assets, else pilot list.</summary>
    public static async Task<IReadOnlyList<string>> ResolveAsync(
        IndexDeskDbContext dbContext,
        string? configTickers,
        bool excludeBenchmarks = true,
        CancellationToken cancellationToken = default
    )
    {
        var fromConfig = FromList(configTickers);
        if (fromConfig.Count > 0)
        {
            return fromConfig;
        }

        await DatabaseInitializer.MigrateAsync(dbContext, cancellationToken);

        var query = dbContext.Assets.Where(a => a.IsActive);
        if (excludeBenchmarks)
        {
            // Benchmarks are ingested by dedicated refresh paths, not the B3 close chain.
            var benchmarkNames = BenchmarkCatalog.All.Keys.ToList();
            query = query.Where(a => !benchmarkNames.Contains(a.Ticker));
        }

        var tickers = await query
            .OrderBy(a => a.Ticker)
            .Select(a => a.Ticker)
            .ToListAsync(cancellationToken);

        return tickers.Count > 0 ? tickers : PilotFallback;
    }
}
