using System.Net;
using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class BcbSeriesClientTests
{
    [Fact]
    public async Task GetSeriesAsync_WithValidJson_ParsesPointsAndSortsByDate()
    {
        // Arrange
        var json = """
            [
                { "data": "05/01/2026", "valor": "0.055131" },
                { "data": "02/01/2026", "valor": "0.055131" }
            ]
            """;

        var handler = TestHttpMessageHandler.CreateJson(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bcb.gov.br/"),
        };
        var client = new BcbSeriesClient(httpClient, NullLogger<BcbSeriesClient>.Instance);

        // Act
        var result = await client.GetSeriesAsync(
            12,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 10)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Date.Should().Be(new DateOnly(2026, 1, 2));
        result.Value[1].Date.Should().Be(new DateOnly(2026, 1, 5));
        result.Value[0].Value.Should().Be(0.055131m);
    }

    [Fact]
    public async Task GetSeriesAsync_WhenHttpError_ReturnsFailure()
    {
        // Arrange
        var handler = TestHttpMessageHandler.CreateStatusCode(HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bcb.gov.br/"),
        };
        var client = new BcbSeriesClient(httpClient, NullLogger<BcbSeriesClient>.Instance);

        // Act
        var result = await client.GetSeriesAsync(
            12,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 10)
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Bcb.HttpError");
    }

    [Fact]
    public async Task GetSeriesAsync_EmptyResponse_ReturnsEmptySuccess()
    {
        // Arrange
        var handler = TestHttpMessageHandler.CreateJson("[]");
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.bcb.gov.br/"),
        };
        var client = new BcbSeriesClient(httpClient, NullLogger<BcbSeriesClient>.Instance);

        // Act
        var result = await client.GetSeriesAsync(
            12,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 10)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
