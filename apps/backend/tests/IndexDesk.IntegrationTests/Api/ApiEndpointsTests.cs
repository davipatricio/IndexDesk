using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IndexDesk.IntegrationTests.Api;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    private sealed record PagedAssetsResponse(
        IReadOnlyList<JsonElement> Items,
        int Page,
        int PageSize,
        long TotalCount
    );

    public ApiEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RootEndpoint_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("IndexDesk Modular API");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProvidersHealth_ReturnsAggregateStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/providers/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
        content.Should().Contain("generatedAt");
        content.Should().Contain("summary");
        content.Should().Contain("providers");
    }

    [Fact]
    public async Task GetAssets_ReturnsPaginatedCatalog()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/assets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("items");
        content.Should().Contain("totalCount");
    }

    [Fact]
    public async Task GetAssets_ViaSearchQuery_ReturnsAValidEmptyPageWhenNoRowsMatch()
    {
        // The catalog is local database state; integration tests must not require a seeded ticker.
        var response = await _client.GetAsync("/api/v1/assets?search=NO_SUCH_TICKER");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedAssetsResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().BeEmpty();
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAssetByTicker_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/assets/NO_SUCH_TICKER");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAssetQuotes_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/assets/NO_SUCH_TICKER/quotes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAssetPerformance_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/v1/assets/NO_SUCH_TICKER/performance");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Backtest_WhenAssetIsMissing_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/analytics/backtest",
            new
            {
                initialAmount = 10_000,
                monthlyContribution = 500,
                allocations = new[] { new { ticker = "NO_SUCH_TICKER", weightPercent = 100 } },
                from = "2020-01-01",
                to = "2024-01-01",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Analytics.AssetNotFound");
    }

    [Fact]
    public async Task RealYield_CalculatesCorrectly()
    {
        // Act
        var response = await _client.GetAsync(
            "/api/v1/analytics/real-yield?nominalRate=12&inflationRate=4"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("7.6923");
    }

    [Fact]
    public async Task AuthSignUp_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange: invalid email fails validation before any DB access.
        var payload = new
        {
            email = "not-an-email",
            password = "SecurePassword123!",
            fullName = "Test User",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signup", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AuthSignUp_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange: password too short fails validation before any DB access.
        var payload = new
        {
            email = "user@exemplo.com",
            password = "short",
            fullName = "Test User",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signup", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
