using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Resolves the explicit-provider backfill targets (<c>--provider yahoo|tv|infomoney</c>).
/// InfoMoney stays outside the automatic fallback chain by design — it is reachable
/// only through this explicit path (SECONDARY source, raw prices without adjclose).
/// </summary>
public sealed class SidecarProviderDirectory
{
    private readonly Dictionary<string, IMarketDataClient> _clients = new(
        StringComparer.OrdinalIgnoreCase
    );

    public SidecarProviderDirectory(
        BrapiClient brapi,
        YfinanceSidecarClient yahoo,
        TradingViewSidecarClient tradingView,
        InfoMoneySidecarClient infoMoney
    )
    {
        _clients["brapi"] = brapi;
        _clients["yahoo"] = yahoo;
        _clients["tv"] = tradingView;
        _clients["infomoney"] = infoMoney;
    }

    /// <summary>Canonical alias map (exposed for tests).</summary>
    internal static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["brapi"] = "brapi",
        ["b3"] = "brapi",
        ["yahoo"] = "yahoo",
        ["yf"] = "yahoo",
        ["yahoosidecar"] = "yahoo",
        ["tv"] = "tv",
        ["tradingview"] = "tv",
        ["tradingviewsidecar"] = "tv",
        ["infomoney"] = "infomoney",
        ["im"] = "infomoney",
    };

    public static string SupportedNames => "brapi|yahoo|tv|infomoney";

    public static bool TryNormalize(string? provider, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(provider))
            return false;
        return Aliases.TryGetValue(provider.Trim(), out var resolved)
            && ((canonical = resolved) != null);
    }

    public Result<IMarketDataClient> Resolve(string provider)
    {
        if (!TryNormalize(provider, out var canonical))
        {
            return Result<IMarketDataClient>.Failure(
                Error.Validation(
                    "Backfill.UnknownProvider",
                    $"Unknown provider '{provider}'. Supported: {SupportedNames}"
                )
            );
        }

        return Result<IMarketDataClient>.Success(_clients[canonical]);
    }

    public async Task<Result<IReadOnlyList<NormalizedQuote>>> FetchQuotesFromProviderAsync(
        string provider,
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        var resolved = Resolve(provider);
        if (resolved.IsFailure)
        {
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(resolved.Error);
        }

        var client = resolved.Value;
        if (!client.SupportsTicker(ticker))
        {
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Validation(
                    "Provider.UnsupportedTicker",
                    $"{client.ProviderName} does not support ticker {ticker}"
                )
            );
        }

        return await client.GetHistoricalQuotesAsync(ticker, startDate, endDate, cancellationToken);
    }
}
