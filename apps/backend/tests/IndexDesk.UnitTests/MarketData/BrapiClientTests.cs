using System.Net;
using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class BrapiClientTests
{
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    [Fact]
    public async Task GetHistoricalQuotesAsync_WithValidJson_ParsesQuotesCorrectly()
    {
        // Arrange
        var json = """
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

        var handler = TestHttpMessageHandler.CreateJson(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://brapi.dev/api/"),
        };
        var client = new BrapiClient(httpClient, _config, NullLogger<BrapiClient>.Instance);

        // Act
        var result = await client.GetHistoricalQuotesAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Ticker.Should().Be("MXRF11");
        result.Value[0].Close.Should().Be(10.55m);
        result.Value[0].SourceProvider.Should().Be("Brapi");
    }

    [Fact]
    public async Task GetHistoricalQuotesAsync_WhenRateLimited429_ReturnsRateLimitError()
    {
        // Arrange
        var handler = TestHttpMessageHandler.CreateStatusCode(HttpStatusCode.TooManyRequests);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://brapi.dev/api/"),
        };
        var client = new BrapiClient(httpClient, _config, NullLogger<BrapiClient>.Instance);

        // Act
        var result = await client.GetHistoricalQuotesAsync(
            "VWRA11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Brapi.RateLimit");
    }

    [Fact]
    public async Task GetDividendsAsync_WithCashDividends_ParsesDividendsCorrectly()
    {
        // Arrange
        var json = """
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

        var handler = TestHttpMessageHandler.CreateJson(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://brapi.dev/api/"),
        };
        var client = new BrapiClient(httpClient, _config, NullLogger<BrapiClient>.Instance);

        // Act
        var result = await client.GetDividendsAsync("MXRF11");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Rate.Should().Be(0.11m);
        result.Value[0].DividendType.Should().Be("Rendimento");
    }
}
