using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the three sidecar-backed IMarketDataClient implementations.
/// A fake shell script plays the sidecar process; the real CLI's --fixture mode is
/// exercised separately by the Python suite (tools/providers/sidecar/tests).
/// </summary>
public class SidecarClientsTests : IDisposable
{
    private readonly string _tempDir;

    public SidecarClientsTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "sidecar-client-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    private static SidecarProcessRunner NewRunner(string scriptPath) =>
        new(
            "/bin/bash",
            new[] { "-lc", $"exec bash '{scriptPath}' \"$@\"", "sidecar" },
            TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );

    private string WriteScript(string name, string body)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, body);
        return path;
    }

    private const string QuoteLines = """
        #!/usr/bin/env bash
        cat <<'EOF'
        {"ticker":"PETR4.SA","date":"2026-08-20","open":30.0,"high":31.0,"low":29.5,"close":30.7,"adj_close":31.2,"volume":1000}
        {"ticker":"PETR4.SA","date":"2026-08-21","open":30.5,"high":31.4,"low":30.1,"close":31.0,"adj_close":31.5,"volume":1200}
        EOF
        """;

    private const string DividendLine = """
        #!/usr/bin/env bash
        echo '{"ticker":"PETR4.SA","date":"2026-08-01","rate":0.52,"type":"DIVIDEND"}'
        """;

    // ---------- YfinanceSidecarClient ----------

    [Fact]
    public async Task YfClient_HappyPath_MapsQuoteFieldsIncludingAdjClose()
    {
        var client = new YfinanceSidecarClient(
            NewRunner(WriteScript("yf.sh", QuoteLines)),
            NullLogger<YfinanceSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "PETR4",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Ticker.Should().Be("PETR4");
        result.Value[0].Date.Should().Be(new DateOnly(2026, 8, 20));
        result.Value[0].Open.Should().Be(30.0m);
        result.Value[0].High.Should().Be(31.0m);
        result.Value[0].Low.Should().Be(29.5m);
        result.Value[0].Close.Should().Be(30.7m);
        result.Value[0].AdjClose.Should().Be(31.2m); // distinct from close: mapped, not overwritten
        result.Value[0].Volume.Should().Be(1000m);
        result.Value[0].SourceProvider.Should().Be("YahooSidecar");
        result.Value.Select(q => q.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task YfClient_Dividends_MapToNormalizedDividend()
    {
        var client = new YfinanceSidecarClient(
            NewRunner(WriteScript("yfd.sh", DividendLine)),
            NullLogger<YfinanceSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetDividendsAsync("petr4");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Ticker.Should().Be("PETR4"); // requested ticker normalized upper
        result.Value[0].ComDate.Should().Be(new DateOnly(2026, 8, 1));
        result.Value[0].PaymentDate.Should().Be(new DateOnly(2026, 8, 1));
        result.Value[0].Rate.Should().Be(0.52m);
        result.Value[0].Currency.Should().Be("BRL");
        result.Value[0].SourceProvider.Should().Be("YahooSidecar");
    }

    [Fact]
    public async Task YfClient_WhenExitThree_ReturnsFetchFailed()
    {
        var script = WriteScript(
            "yf3.sh",
            """
            #!/usr/bin/env bash
            echo '{"error":{"code":"Fetch.Failed","message":"yahoo down"}}' >&2
            exit 3
            """
        );
        var client = new YfinanceSidecarClient(
            NewRunner(script),
            NullLogger<YfinanceSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "PETR4",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sidecar.FetchFailed");
    }

    [Fact]
    public async Task YfClient_WithMalformedNdjsonLine_ReturnsParseError()
    {
        var script = WriteScript(
            "yfbad.sh",
            """
            #!/usr/bin/env bash
            echo '{"ticker": broken json'
            """
        );
        var client = new YfinanceSidecarClient(
            NewRunner(script),
            NullLogger<YfinanceSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "PETR4",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sidecar.ParseError");
    }

    // ---------- TradingViewSidecarClient ----------

    [Fact]
    public async Task TvClient_HappyPath_MapsCandlesAndPrefixesBmfbovespa()
    {
        var script = WriteScript(
            "tv.sh",
            """
            #!/usr/bin/env bash
            for arg in "$@"; do echo "[arg] $arg" >&2; done
            cat <<'EOF'
            {"ticker":"BMFBOVESPA:BOVA11","date":"2026-08-21","open":120.1,"high":121.0,"low":119.5,"close":120.7,"adj_close":120.7,"volume":5400}
            EOF
            """
        );
        var client = new TradingViewSidecarClient(
            NewRunner(script),
            EmptyConfig(),
            NullLogger<TradingViewSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "BOVA11",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Ticker.Should().Be("BOVA11");
        result.Value[0].Close.Should().Be(120.7m);
        result.Value[0].AdjClose.Should().Be(120.7m); // TV series is unadjusted
        result.Value[0].SourceProvider.Should().Be("TradingView");
    }

    [Fact]
    public void TvClient_Arguments_UseTvSymbolAndClampedBars()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Providers:TradingView:Cookie"] = "secret-cookie",
                }
            )
            .Build();
        var runner = NewRunner(WriteScript("noop.sh", "#!/usr/bin/env bash\n"));
        var client = new TradingViewSidecarClient(
            runner,
            config,
            NullLogger<TradingViewSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var args = client.HistoryArguments(
            "bova11",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 8, 21)
        );

        var joined = string.Join(' ', args);
        joined.Contains("--symbol BMFBOVESPA:BOVA11").Should().BeTrue(); // bare ticker prefixed, lowercased input normalized
        TradingViewSidecarClient.ToTvSymbol("NASDAQ:AAPL").Should().Be("NASDAQ:AAPL"); // explicit pairs pass through

        var symbolIndex = args.ToList().IndexOf("--symbol");
        args[symbolIndex + 1].Should().NotContain("secret-cookie"); // cookie never rides along the symbol
        args.Should().Contain("--cookie").And.Contain("secret-cookie"); // but is forwarded to the child argv
        TradingViewSidecarClient
            .EstimateBars(new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 21))
            .Should()
            .BeInRange(5, 5000);
        TradingViewSidecarClient
            .EstimateBars(new DateOnly(2000, 1, 1), new DateOnly(2026, 8, 21))
            .Should()
            .BeInRange(5, 5000);
    }

    [Fact]
    public async Task TvClient_AuthFailureExitMapsToTradingViewAuthFailed()
    {
        var script = WriteScript(
            "tvauth.sh",
            """
            #!/usr/bin/env bash
            echo '{"error":{"code":"Fetch.Failed","message":"authentication failed: invalid session cookie"}}' >&2
            exit 3
            """
        );
        var client = new TradingViewSidecarClient(
            NewRunner(script),
            EmptyConfig(),
            NullLogger<TradingViewSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "BOVA11",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TradingView.AuthFailed");
    }

    [Fact]
    public async Task TvClient_Dividends_ReturnEmptySuccessWithoutSpawning()
    {
        var neverSpawn = new SidecarProcessRunner(
            "/nonexistent/uv-must-not-run",
            Array.Empty<string>(),
            TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );
        var client = new TradingViewSidecarClient(
            neverSpawn,
            EmptyConfig(),
            NullLogger<TradingViewSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetDividendsAsync("BOVA11");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ---------- InfoMoneySidecarClient ----------

    private const string InfoMoneyEnvGuard = """
        #!/usr/bin/env bash
        if [ -z "$INFOMONEY_SUBSCRIPTION_KEY" ]; then
          echo '{"error":{"code":"Fetch.Failed","message":"key missing"}}' >&2
          exit 3
        fi
        cat <<'EOF'
        {"ticker":"MGLU3","date":"2026-08-20","open":8.6,"high":9.6,"low":8.1,"close":9.1,"adj_close":9.1,"volume":900}
        {"ticker":"MGLU3","date":"2026-08-21","open":9.0,"high":9.9,"low":8.9,"close":9.4,"adj_close":9.4,"volume":1100}
        EOF
        """;

    [Fact]
    public async Task ImClient_HappyPath_ForwardsKeyViaEnvironmentAndMapsQuotes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Providers:InfoMoney:SubscriptionKeys:0"] = "test-key-123",
                }
            )
            .Build();
        var client = new InfoMoneySidecarClient(
            NewRunner(WriteScript("im.sh", InfoMoneyEnvGuard)),
            config,
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(config ?? EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "MGLU3",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[1].Close.Should().Be(9.4m);
        result.Value[1].AdjClose.Should().Be(9.4m); // unadjusted series mirrors close
        result.Value[1].SourceProvider.Should().Be("InfoMoney");
    }

    [Fact]
    public async Task ImClient_WithoutConfiguredKey_FailsBeforeSpawning()
    {
        var neverSpawn = new SidecarProcessRunner(
            "/nonexistent/uv-must-not-run",
            Array.Empty<string>(),
            TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );
        var client = new InfoMoneySidecarClient(
            neverSpawn,
            EmptyConfig(),
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        var quotes = await client.GetHistoricalQuotesAsync(
            "MGLU3",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );
        var dividends = await client.GetDividendsAsync("MGLU3");

        quotes.IsFailure.Should().BeTrue();
        quotes.Error.Code.Should().Be("InfoMoney.NoApiKey");
        dividends.Error.Code.Should().Be("InfoMoney.NoApiKey");
    }

    [Fact]
    public async Task ImClient_WafBlockedEnvelope_MapsToScrapeWafBlocked()
    {
        var script = WriteScript(
            "imwaf.sh",
            """
            #!/usr/bin/env bash
            echo '<html>Acesso Bloqueado</html>' >&2
            echo '{"error":{"code":"Scrape.WafBlocked","message":"Akamai blocked (HTTP 403)"}}' >&2
            exit 3
            """
        );
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Providers:InfoMoney:SubscriptionKeys:0"] = "k" }
            )
            .Build();
        var client = new InfoMoneySidecarClient(
            NewRunner(script),
            config,
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(config ?? EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetHistoricalQuotesAsync(
            "MGLU3",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Scrape.WafBlocked");
    }

    [Fact]
    public async Task ImClient_BadKeyEnvelope_MapsToAuthFailed()
    {
        var script = WriteScript(
            "imauth.sh",
            """
            #!/usr/bin/env bash
            echo '{"error":{"code":"Scrape.AuthFailed","message":"HTTP 401"}}' >&2
            exit 3
            """
        );
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Providers:InfoMoney:SubscriptionKeys:0"] = "wrong",
                }
            )
            .Build();
        var client = new InfoMoneySidecarClient(
            NewRunner(script),
            config,
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(config ?? EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetDividendsAsync("MGLU3");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InfoMoney.AuthFailed");
    }

    [Fact]
    public async Task ImClient_Dividends_PassthroughB3TypeVocabulary()
    {
        var script = WriteScript(
            "imdiv.sh",
            """
            #!/usr/bin/env bash
            echo '{"ticker":"MGLU3","date":"2026-05-06","rate":0.15,"type":"DIVIDENDO"}'
            echo '{"ticker":"MGLU3","date":"2026-02-11","rate":0.42,"type":"JRSCAPPROPRIO"}'
            """
        );
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Providers:InfoMoney:SubscriptionKeys:0"] = "k" }
            )
            .Build();
        var client = new InfoMoneySidecarClient(
            NewRunner(script),
            config,
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(config ?? EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetDividendsAsync("MGLU3");

        result.IsSuccess.Should().BeTrue();
        result.Value[0].DividendType.Should().Be("JRSCAPPROPRIO"); // B3-native vocabulary preserved (IR rules)
        result.Value[1].DividendType.Should().Be("DIVIDENDO");
        result.Value[1].Rate.Should().Be(0.15m);
    }

    // ---------- shared behavior ----------

    [Theory]
    [InlineData("^BVSP", false)]
    [InlineData("USDBRL=X", false)]
    [InlineData("", false)]
    [InlineData("MGLU3", true)]
    [InlineData("PETR4", true)]
    public void ImClient_SupportsTicker_AcceptsOnlyBareB3Tickers(string ticker, bool expected)
    {
        var client = new InfoMoneySidecarClient(
            NewRunner(WriteScript($"noop-{Guid.NewGuid():N}.sh", "#!/usr/bin/env bash\n")),
            EmptyConfig(),
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(EmptyConfig()),
            ResilienceTestKit.NewResilience()
        );

        client.SupportsTicker(ticker).Should().Be(expected);
    }

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();
}
