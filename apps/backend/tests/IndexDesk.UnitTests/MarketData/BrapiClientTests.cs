using System.Net;
using System.Text;
using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Resilience;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the Brapi client — including the Fase-4 key-pool wiring: token
/// acquired per attempt from the pool, 429/401/403 reported by key index, and pool
/// exhaustion short-circuiting before any HTTP call (chain falls through).
/// </summary>
public class BrapiClientTests
{
    private static BrapiClient CreateClient(
        HttpMessageHandler handler,
        IConfiguration? config = null,
        IApiKeyPool? pool = null
    ) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://brapi.dev/api/") },
            config ?? new ConfigurationBuilder().Build(),
            NullLogger<BrapiClient>.Instance,
            pool ?? ResilienceTestKit.NewPool(),
            ResilienceTestKit.NewResilience()
        );

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private const string HistoricalJson = """
        {
            "results": [
                {
                    "symbol": "MXRF11",
                    "historicalDataPrice": [
                        {
                            "date": 1707955200,
                            "open": 10.50,
                            "high": 10.60,
                            "low": 10.45,
                            "close": 10.55,
                            "adjustedClose": 10.55,
                            "volume": 1234567
                        }
                    ]
                }
            ]
        }
        """;

    private const string DividendJson = """
        {
            "results": [
                {
                    "symbol": "MXRF11",
                    "dividendsData": {
                        "cashDividends": [
                            {
                                "rate": 0.11,
                                "paymentDate": "2024-02-15T00:00:00.000Z",
                                "approvedOn": "2024-01-31T00:00:00.000Z",
                                "lastDatePrior": "2024-01-31T00:00:00.000Z",
                                "relatedTo": "Rendimento"
                            }
                        ]
                    }
                }
            ]
        }
        """;

    [Fact]
    public async Task GetHistoricalQuotesAsync_WithValidJson_ParsesQuotesCorrectly()
    {
        var handler = TestHttpMessageHandler.CreateJson(HistoricalJson);
        var client = CreateClient(handler);

        var result = await client.GetHistoricalQuotesAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Ticker.Should().Be("MXRF11");
        result.Value[0].Close.Should().Be(10.55m);
        result.Value[0].SourceProvider.Should().Be("Brapi");
    }

    [Fact]
    public async Task GetHistoricalQuotesAsync_WhenRateLimited429_ReturnsRateLimitError()
    {
        var handler = TestHttpMessageHandler.CreateStatusCode(HttpStatusCode.TooManyRequests);
        var client = CreateClient(handler);

        var result = await client.GetHistoricalQuotesAsync(
            "VWRA11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Brapi.RateLimit");
    }

    [Fact]
    public async Task GetDividendsAsync_WithCashDividends_ParsesDividendsCorrectly()
    {
        var handler = TestHttpMessageHandler.CreateJson(DividendJson);
        var client = CreateClient(handler);

        var result = await client.GetDividendsAsync("MXRF11");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Rate.Should().Be(0.11m);
        result.Value[0].DividendType.Should().Be("Rendimento");
    }

    // ---------- Fase 4: key pool ----------

    [Fact]
    public async Task WithPooledKey_AppendsTokenAndReportsSuccess()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:Brapi:ApiKeys:0"] = "pooled-key-1" }
        );
        var handler = new Helpers.CapturingHttpHandler(_ => Json(HistoricalJson));
        var client = CreateClient(handler, pool: pool);

        var result = await client.GetHistoricalQuotesAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        result.IsSuccess.Should().BeTrue();
        handler.RequestUrls.Should().ContainSingle();
        handler.RequestUrls[0].Should().Contain("token=pooled-key-1");

        var stats = pool.Snapshot().Single(s => s.Provider == "Brapi");
        stats.KeyIndex.Should().Be(0);
        stats.SuccessCount.Should().Be(1);
        stats.State.Should().NotBe("Disabled");
    }

    [Fact]
    public async Task RateLimited_ReportsCooldownByKeyIndex()
    {
        var pool = ResilienceTestKit.NewPool(
            // Legacy single-key config binds as ApiKeys__0.
            new Dictionary<string, string?> { ["Providers:Brapi:ApiKey"] = "legacy-key" }
        );
        var handler = new Helpers.CapturingHttpHandler(_ => new HttpResponseMessage(
            HttpStatusCode.TooManyRequests
        ));
        var client = CreateClient(handler, pool: pool);

        var result = await client.GetDailyBatchQuotesAsync(["PETR4"], new DateOnly(2026, 8, 21));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Brapi.RateLimit");

        var stats = pool.Snapshot().Single(s => s.Provider == "Brapi");
        stats.RateLimitedCount.Should().Be(1);
        stats.SuccessCount.Should().Be(0);

        // The cooled-down key is out of rotation: Acquire returns null now.
        pool.HasKeys("Brapi").Should().BeTrue();
        pool.Acquire("Brapi").Should().BeNull();
    }

    [Fact]
    public async Task Unauthorized_ReportsInvalidAndMapsAuthFailed()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:Brapi:ApiKeys:0"] = "bad-key" }
        );
        var handler = new Helpers.CapturingHttpHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = CreateClient(handler, pool: pool);

        var result = await client.GetDividendsAsync("PETR4");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Brapi.AuthFailed");

        var stats = pool.Snapshot().Single(s => s.Provider == "Brapi");
        stats.InvalidCount.Should().Be(1);
        pool.Acquire("Brapi").Should().BeNull(); // disabled until process restart
    }

    [Fact]
    public async Task PoolExhausted_ShortCircuitsBeforeAnyHttpRequest()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:Brapi:ApiKeys:0"] = "k0" }
        );
        // Exhaust the only key up front (simulated 429 earlier in the run).
        pool.Report("Brapi", "k0", KeyResult.RateLimited);
        var handler = new CapturingHttpHandler(_ =>
            throw new InvalidOperationException("HTTP must not be reached when pool is exhausted")
        );
        var client = CreateClient(handler, pool: pool);

        var batch = await client.GetDailyBatchQuotesAsync(["PETR4"], new DateOnly(2026, 8, 21));
        var quotes = await client.GetHistoricalQuotesAsync(
            "PETR4",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 30)
        );

        batch.IsFailure.Should().BeTrue();
        batch.Error.Code.Should().Be("Provider.PoolExhausted"); // soft → chain falls through
        quotes.Error.Code.Should().Be("Provider.PoolExhausted");
        handler.RequestUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutConfiguredKeys_RunsKeyless_LegacyBehavior()
    {
        var handler = new Helpers.CapturingHttpHandler(url =>
            url.Contains("token=")
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : Json(HistoricalJson)
        );
        var emptyPool = ResilienceTestKit.NewPool();
        var client = CreateClient(handler, pool: emptyPool);

        var result = await client.GetHistoricalQuotesAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        result.IsSuccess.Should().BeTrue();
        handler.RequestUrls.Single().Should().NotContain("token=");
        emptyPool.HasKeys("Brapi").Should().BeFalse();
    }
}
