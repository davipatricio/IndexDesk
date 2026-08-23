using IndexDesk.Modules.MarketData.Clients;
using Microsoft.Extensions.Configuration;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>Raw fetchers for the manager feeds (HTML/CSV/XLSX). No parsing here —
/// parsers are pure functions so they can be unit-tested against local fixtures.</summary>
public sealed class ISharesHoldingsFeed
{
    private readonly HttpClient _httpClient;

    public ISharesHoldingsFeed(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Product page HTML for a national ETF. The ticker → product-page mapping is
    /// configured (<c>Providers:Ishares:Products:{TICKER}</c>, absolute URL or path);
    /// the ajax CSV link inside rotates and is extracted by the parser.
    /// </summary>
    public async Task<string> GetProductPageHtmlAsync(
        string ticker,
        string productPage,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(productPage))
        {
            throw new HoldingsLayoutException(
                $"No iShares product page configured for {ticker} "
                    + "(Providers:Ishares:Products:{TICKER})"
            );
        }

        var url = productPage.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? productPage
            : new Uri(_httpClient.BaseAddress!, productPage).ToString();

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string> DownloadCsvAsync(
        string csvUrl,
        CancellationToken cancellationToken = default
    )
    {
        using var response = await _httpClient.GetAsync(csvUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public sealed class SpdrHoldingsFeed
{
    private readonly HttpClient _httpClient;

    public SpdrHoldingsFeed(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<Stream> DownloadXlsxAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        // SSGA's CDN is case-sensitive: only the all-lowercase ticker resolves
        // ("...holdings-daily-us-en-spy.xlsx"); uppercase returns a hard 404.
        var url =
            $"library-content/products/fund-data/etfs/us/holdings-daily-us-en-"
            + $"{ticker.Trim().ToLowerInvariant()}.xlsx";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        // Buffered copy: ClosedXML needs a seekable stream.
        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }
}

/// <summary>It Now composition data (Itaú Asset ETFs: BOVV11, SPXI11, IVVB11...).
/// Real host is <c>www.itnow.com.br</c> — the apex NXDOMAINs (recon Fase 0.5). Primary
/// source is the structured JSON API discovered in the page's own HAR; the HTML table is
/// the fallback.
/// <para>
/// Transport: Akamai TLS-fingerprints this host — native HttpClient gets 403
/// "Access Denied" even with full browser headers while
/// <c>curl_cffi impersonate="chrome"</c> gets 200 (measured 2026-08-23). Both
/// methods therefore route through <see cref="ISidecarHttp"/> by default;
/// <c>Providers:Holdings:ItNow:Transport=native</c> restores direct HttpClient.
/// Without a registered sidecar the feed degrades to native so hand-built test
/// instances keep working.
/// </para></summary>
public sealed class ItNowHoldingsFeed
{
    private readonly HttpClient _httpClient;
    private readonly ISidecarHttp? _sidecarHttp;

    public ItNowHoldingsFeed(
        HttpClient httpClient,
        ISidecarHttp? sidecarHttp = null,
        IConfiguration? configuration = null
    )
    {
        _httpClient = httpClient;
        _sidecarHttp = configuration
            ?["Providers:Holdings:ItNow:Transport"]?.Trim()
            .ToLowerInvariant()
            is "native"
            ? null
            : sidecarHttp;
    }

    public async Task<string> GetCompositionHtmlAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        var path = $"{ticker.Trim().ToLowerInvariant()}/composicao/";
        if (_sidecarHttp is not null)
        {
            return await _sidecarHttp.FetchTextAsync(
                new SidecarHttpRequest(AbsoluteUrl(path), TimeoutSeconds: SidecarTimeoutSeconds),
                cancellationToken
            );
        }

        using var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>POSTs the structured holdings API the composition page itself calls
    /// (<c>/history-api-json/?type=composicoes-indices&amp;fundo={fundCode}</c>). The fund
    /// code (ISIN, e.g. BRBOVVCTF009) rotates per fund and is resolved by the caller from
    /// the page HTML / config map — never hardcoded here.</summary>
    public async Task<string> PostCompositionJsonAsync(
        string ticker,
        string fundCode,
        CancellationToken cancellationToken = default
    )
    {
        var path =
            $"history-api-json/?type=composicoes-indices&fundo={Uri.EscapeDataString(fundCode)}";
        if (_sidecarHttp is not null)
        {
            return await _sidecarHttp.FetchTextAsync(
                new SidecarHttpRequest(
                    AbsoluteUrl(path),
                    Method: "POST",
                    TimeoutSeconds: SidecarTimeoutSeconds
                ),
                cancellationToken
            );
        }

        using var response = await _httpClient.PostAsync(path, content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>Network budget per sidecar request; the runner-level process timeout
    /// (<c>Providers:Sidecar:TimeoutSeconds</c>) stays the outer bound.</summary>
    private const int SidecarTimeoutSeconds = 20;

    private string AbsoluteUrl(string relativePath)
    {
        if (_httpClient.BaseAddress is { } baseAddress)
        {
            return new Uri(baseAddress, relativePath).ToString();
        }

        return relativePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : throw new InvalidOperationException(
                "ItNow sidecar transport needs HttpClient.BaseAddress or an absolute URL."
            );
    }
}

/// <summary>Investo ETF detail page (BTG ETFs: WRLD11, BDEF11, ALUG11...).</summary>
public sealed class InvestoHoldingsFeed
{
    private readonly HttpClient _httpClient;

    public InvestoHoldingsFeed(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> GetEtfHtmlAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        var url = $"etf/{ticker.Trim().ToLowerInvariant()}/";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
