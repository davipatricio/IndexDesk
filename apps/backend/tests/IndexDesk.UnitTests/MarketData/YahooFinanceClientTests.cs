using System.Net;
using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class YahooFinanceClientTests
{
    [Fact]
    public async Task GetHistoricalQuotesAsync_WithValidChartJson_ParsesQuotesCorrectly()
    {
        // Arrange
        var json = """
            {
                "chart": {
                    "result": [
                        {
                            "meta": { "symbol": "VWRA11.SA" },
                            "timestamp": [1707955200],
                            "indicators": {
                                "quote": [
                                    {
                                        "open": [140.50],
                                        "high": [142.00],
                                        "low": [140.00],
                                        "close": [141.50],
                                        "volume": [35000]
                                    }
                                ],
                                "adjclose": [
                                    {
                                        "adjclose": [141.50]
                                    }
                                ]
                            }
                        }
                    ],
                    "error": null
                }
            }
            """;

        var handler = TestHttpMessageHandler.CreateJson(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var client = new YahooFinanceClient(httpClient, NullLogger<YahooFinanceClient>.Instance);

        // Act
        var result = await client.GetHistoricalQuotesAsync(
            "VWRA11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Ticker.Should().Be("VWRA11");
        result.Value[0].Close.Should().Be(141.50m);
        result.Value[0].SourceProvider.Should().Be("YahooFinance");
    }

    [Fact]
    public async Task GetHistoricalQuotesAsync_WhenNullValuesExistInArray_SkipsCorruptedEntries()
    {
        // Arrange: Second entry has null open/high/low/close (e.g. trading halt)
        var json = """
            {
                "chart": {
                    "result": [
                        {
                            "meta": { "symbol": "GOLD11.SA" },
                            "timestamp": [1707955200, 1708041600],
                            "indicators": {
                                "quote": [
                                    {
                                        "open": [11.50, null],
                                        "high": [11.80, null],
                                        "low": [11.40, null],
                                        "close": [11.75, null],
                                        "volume": [50000, null]
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
            """;

        var handler = TestHttpMessageHandler.CreateJson(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://query1.finance.yahoo.com/"),
        };
        var client = new YahooFinanceClient(httpClient, NullLogger<YahooFinanceClient>.Instance);

        // Act
        var result = await client.GetHistoricalQuotesAsync(
            "GOLD11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Close.Should().Be(11.75m);
    }
}
