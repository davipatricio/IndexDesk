using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Resilience;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>Offline tests for the AwesomeAPI FX client (fake transport, no network).</summary>
public class AwesomeApiClientTests
{
    private const string LastPayload = """
        {
          "USDBRL": {"high":"5.52","low":"5.44","open":"5.45","bid":"5.5","ask":"5.502","timestamp":"1755976800"},
          "EURBRL": {"high":"6.45","low":"6.35","open":"6.40","bid":"6.42","ask":"6.422","timestamp":"1755976800"},
          "BTCBRL": {"high":"700000","low":"680000","open":"690000","bid":"695000","ask":"696000","timestamp":"1755976800"}
        }
        """;

    private static AwesomeApiClient CreateClient(string payload, IApiKeyPool? pool = null)
    {
        var httpClient = new HttpClient(Helpers.TestHttpMessageHandler.CreateJson(payload))
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/"),
        };
        return new AwesomeApiClient(
            httpClient,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<AwesomeApiClient>.Instance,
            pool ?? ResilienceTestKit.NewPool(),
            ResilienceTestKit.NewResilience()
        );
    }

    [Fact]
    public async Task GetLastAsync_MapsBidAskAndDate()
    {
        var client = CreateClient(LastPayload);

        var result = await client.GetLastAsync(new[] { "USD-BRL", "EUR-BRL", "BTC-BRL" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        var usd = result.Value.Single(r => r.Pair == "USD-BRL");
        usd.Bid.Should().Be(5.5m); // bid doubles as close proxy
        usd.Ask.Should().Be(5.502m);
        usd.Open.Should().Be(5.45m);
        usd.TimestampEpochSeconds.Should().Be(1755976800);
        usd.SourceProvider.Should().Be("AwesomeApi");
    }

    [Fact]
    public async Task GetLastAsync_EmptyPairs_ReturnsEmptyWithoutRequest()
    {
        var client = CreateClient("{}");

        var result = await client.GetLastAsync(Array.Empty<string>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLastAsync_WhenRateLimited429_FailsWithRateLimitAndReportsPool()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:AwesomeApi:Tokens:0"] = "sk_test" }
        );
        var httpClient = new HttpClient(
            Helpers.TestHttpMessageHandler.CreateStatusCode(
                System.Net.HttpStatusCode.TooManyRequests
            )
        )
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/"),
        };
        var client = new AwesomeApiClient(
            httpClient,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<AwesomeApiClient>.Instance,
            pool,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetLastAsync(["USD-BRL"]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AwesomeApi.RateLimit");
        pool.Snapshot().Single(s => s.Provider == "AwesomeApi").RateLimitedCount.Should().Be(1);
        pool.Acquire("AwesomeApi").Should().BeNull(); // cooled down
    }

    [Fact]
    public async Task GetLastAsync_WithPooledToken_AppendsTokenQueryParameter()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?>
            {
                ["Providers:AwesomeApi:Token"] = "sk_legacy", // legacy single binds as Tokens__0
            }
        );
        var handler = new Helpers.CapturingHttpHandler(_ =>
            Helpers.CapturingHttpHandler.Json(System.Net.HttpStatusCode.OK, LastPayload)
        );
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/"),
        };
        var client = new AwesomeApiClient(
            httpClient,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<AwesomeApiClient>.Instance,
            pool,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetLastAsync(["USD-BRL"]);

        result.IsSuccess.Should().BeTrue();
        handler.RequestUrls.Single().Should().Contain("token=sk_legacy");
        pool.Snapshot().Single(s => s.Provider == "AwesomeApi").SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task GetLastAsync_PoolExhausted_FailsSoftWithoutRequest()
    {
        var pool = ResilienceTestKit.NewPool(
            new Dictionary<string, string?> { ["Providers:AwesomeApi:Tokens:0"] = "sk_test" }
        );
        pool.Report("AwesomeApi", "sk_test", KeyResult.Invalid); // disabled until restart
        var httpClient = new HttpClient(
            Helpers.TestHttpMessageHandler.CreateStatusCode(
                System.Net.HttpStatusCode.TooManyRequests
            )
        )
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/"),
        };
        var client = new AwesomeApiClient(
            httpClient,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<AwesomeApiClient>.Instance,
            pool,
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetLastAsync(["USD-BRL"]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Provider.PoolExhausted");
    }

    [Fact]
    public async Task GetLastAsync_ErrorStatus_FailsWithHttpError()
    {
        var httpClient = new HttpClient(
            Helpers.TestHttpMessageHandler.CreateStatusCode(
                System.Net.HttpStatusCode.InternalServerError
            )
        )
        {
            BaseAddress = new Uri("https://economia.awesomeapi.com.br/"),
        };
        var client = new AwesomeApiClient(
            httpClient,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<AwesomeApiClient>.Instance,
            ResilienceTestKit.NewPool(),
            ResilienceTestKit.NewResilience()
        );

        var result = await client.GetLastAsync(new[] { "USD-BRL" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AwesomeApi.HttpError");
    }

    [Fact]
    public async Task GetDailyAsync_ReturnsChronologicalOrder_WithSuffixedKeysCollapsed()
    {
        // /json/daily returns newest-first and keys carry numeric suffixes (USDBRL1...).
        var payload = """
            {
              "USDBRL2": {"high":"5.60","low":"5.50","open":"5.55","bid":"5.56","ask":"5.562","timestamp":"1755890400"},
              "USDBRL1": {"high":"5.70","low":"5.60","open":"5.65","bid":"5.66","ask":"5.662","timestamp":"1755976800"}
            }
            """;
        var client = CreateClient(payload);

        var result = await client.GetDailyAsync(
            "USD-BRL",
            new DateOnly(2026, 8, 23),
            new DateOnly(2026, 8, 24)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(r => r.Date).Should().BeInAscendingOrder();
        result.Value[0].Pair.Should().Be("USD-BRL"); // suffix collapsed to canonical pair
    }

    [Fact]
    public void NormalizePair_CollapsesApiSuffixes()
    {
        AwesomeApiClient.NormalizePair("USDBRL").Should().Be("USD-BRL");
        AwesomeApiClient.NormalizePair("usdbrl3").Should().Be("USD-BRL");
        AwesomeApiClient.NormalizePair("BTCEUR").Should().Be("BTC-EUR");
    }
}
