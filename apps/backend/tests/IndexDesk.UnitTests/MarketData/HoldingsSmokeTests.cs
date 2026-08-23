using System.Net.Http.Headers;
using FluentAssertions;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Ingestion.Holdings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// MANUAL LIVE SMOKE (network allowed). Never runs in CI or plain `dotnet test`: opt in
/// with HOLDINGS_SMOKE=1.
///
///   HOLDINGS_SMOKE=1 dotnet test --filter "FullyQualifiedName~HoldingsSmokeTests"
///
/// Exercises the real feed + parser code paths used by EtfHoldingsSyncService (no DB):
/// iShares product page → rotating ajax CSV → CsvHelper parse; It Now composition page
/// → fund-code extraction → history-api-json JSON (HTML fallback on failure); Investo
/// ETF page → AngleSharp table parse.
/// </summary>
public sealed class HoldingsSmokeFactAttribute : FactAttribute
{
    public HoldingsSmokeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOLDINGS_SMOKE") != "1")
        {
            Skip = "manual live smoke: set HOLDINGS_SMOKE=1 to run against real feeds";
        }
    }
}

public sealed class HoldingsSmokeTests
{
    private static HttpClient NewClient(string baseAddress)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseAddress) };
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"
        );
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }

    /// <summary>Real sidecar transport (uv + curl_cffi chrome), default config.</summary>
    private static ISidecarHttp NewSidecarHttp() =>
        new SidecarHttp(
            new SidecarProcessRunner(
                new ConfigurationBuilder().Build(),
                NullLogger<SidecarProcessRunner>.Instance
            )
        );

    [HoldingsSmokeFact]
    public async Task Smoke_IShares_Bova11_ProductPageToCsv()
    {
        using var httpClient = NewClient("https://www.blackrock.com/");
        var feed = new ISharesHoldingsFeed(httpClient);
        const string productPage =
            "https://www.blackrock.com/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund";

        var html = await feed.GetProductPageHtmlAsync("BOVA11", productPage);
        var csvUrl = ISharesHoldingsParser.ExtractAjaxCsvUrl(html, baseUrl: productPage);
        csvUrl.Should().NotBeNull("the product page must expose its rotating ajax CSV link");
        var csv = await feed.DownloadCsvAsync(csvUrl!, CancellationToken.None);
        var holdings = ISharesHoldingsParser.ParseCsv(csv);

        holdings.Should().NotBeEmpty();
        var top = holdings.OrderByDescending(h => h.WeightPercentage).First();
        Console.WriteLine(
            $"[smoke] iShares BOVA11 rows={holdings.Count} top={top.Ticker} "
                + $"weight={top.WeightPercentage}% csvUrl={csvUrl}"
        );
    }

    [HoldingsSmokeFact]
    public async Task Smoke_ItNow_Bovv11_JsonApiPreferred()
    {
        // WAF note: itnow.com.br TLS-fingerprints non-browser clients — plain
        // HttpClient/curl get 403 "Access Denied"; the sidecar's curl_cffi
        // impersonate=chrome gets 200. This smoke therefore exercises the same
        // sidecar transport (ISidecarHttp) the service uses by default.
        using var httpClient = NewClient("https://www.itnow.com.br/");
        var feed = new ItNowHoldingsFeed(httpClient, NewSidecarHttp());

        IReadOnlyList<ParsedHolding> holdings;
        string source;
        DateOnly? asOf = null;
        var html = await feed.GetCompositionHtmlAsync("BOVV11");
        var fundCode = ItNowJsonParser.TryExtractFundCode(html);
        fundCode.Should().NotBeNullOrEmpty("composition page embeds its own fund code");
        var json = await feed.PostCompositionJsonAsync("BOVV11", fundCode!);
        holdings = ItNowJsonParser.ParseJson(json);
        asOf = ItNowJsonParser.TryGetAsOfDate(json);
        source = "ITNOW_JSON";

        holdings.Should().NotBeEmpty();
        var top = holdings.OrderByDescending(h => h.WeightPercentage).First();
        Console.WriteLine(
            $"[smoke] It Now BOVV11 ({source}, sidecar transport) rows={holdings.Count} "
                + $"as-of={asOf:yyyy-MM-dd} top={top.Ticker} weight={top.WeightPercentage}%"
        );
    }

    [HoldingsSmokeFact]
    public async Task Smoke_Investo_Wrld11_CompositionHtml()
    {
        using var httpClient = NewClient("https://investoetf.com/");
        var feed = new InvestoHoldingsFeed(httpClient);

        var html = await feed.GetEtfHtmlAsync("WRLD11");
        var holdings = HtmlCompositionParser.Parse(html);

        holdings.Should().NotBeEmpty();
        var top = holdings.OrderByDescending(h => h.WeightPercentage).First();
        Console.WriteLine(
            $"[smoke] Investo WRLD11 rows={holdings.Count} top={top.Name} "
                + $"weight={top.WeightPercentage}%"
        );
    }

    [HoldingsSmokeFact]
    public async Task Smoke_HoldingsSyncService_EndToEnd_UpsertsLocalDb()
    {
        // Full service path (fetch → parse → idempotent upsert → sync_job_logs) against
        // the local docker-compose Postgres. Requires etf_holdings to exist (fresh DB via
        // EnsureCreated, or the additive DDL in the Fase 3 registro) and curated assets —
        // tickers absent from `assets` are skipped with a warning by design.
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret";

        var dbContextOptions = new DbContextOptionsBuilder<IndexDeskDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var dbContext = new IndexDeskDbContext(dbContextOptions);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Providers:Ishares:Products:BOVA11"] =
                        "https://www.blackrock.com/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund",
                    ["Providers:Holdings:Spdr:Tickers"] = "SPY",
                    ["Providers:Holdings:ItNow:Tickers"] = "BOVV11",
                    ["Providers:Holdings:Investo:Tickers"] = "WRLD11",
                    ["Providers:Holdings:ItNow:FundCodes:BOVV11"] = "BRBOVVCTF009",
                }
            )
            .Build();

        using var isharesHttp = NewClient("https://www.blackrock.com/");
        using var spdrHttp = NewClient("https://www.ssga.com/");
        using var itNowHttp = NewClient("https://www.itnow.com.br/");
        using var investoHttp = NewClient("https://investoetf.com/");

        var service = new EtfHoldingsSyncService(
            dbContext,
            new ISharesHoldingsFeed(isharesHttp),
            new SpdrHoldingsFeed(spdrHttp),
            // Default transport = sidecar: itnow.com.br 403s native HttpClient.
            new ItNowHoldingsFeed(itNowHttp, NewSidecarHttp(), configuration),
            new InvestoHoldingsFeed(investoHttp),
            configuration,
            NullLogger<EtfHoldingsSyncService>.Instance
        );

        var result = await service.SyncWeeklyAsync();

        result.IsSuccess.Should().BeTrue();
        Console.WriteLine(
            $"[smoke] HoldingsSyncService status={result.Value.Status} "
                + $"sources={result.Value.SourcesSucceeded}/{result.Value.SourcesAttempted} "
                + $"rows={result.Value.TotalRowsUpserted}"
        );
        foreach (var source in result.Value.Sources)
        {
            Console.WriteLine(
                $"[smoke]   {source.Source} {source.Ticker}: {source.Status} "
                    + $"upserted={source.HoldingsUpserted} {source.Error}"
            );
        }

        // WRLD11 is the one curated asset locally — its rows must be persisted.
        result.Value.Sources.Single(s => s.Ticker == "WRLD11").Status.Should().Be("SUCCESS");
        result.Value.TotalRowsUpserted.Should().BeGreaterThan(0);
    }
}
