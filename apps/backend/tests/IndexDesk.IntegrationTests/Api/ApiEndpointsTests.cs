using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IndexDesk.IntegrationTests.Api;

public class ApiEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

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
    public async Task GetAssets_ViaSearchQuery_FiltersByTicker()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/assets?search=WRLD");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("WRLD11");
        content.Should().NotContain("MXRF11");
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
    public async Task AuthLogin_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var payload = new { email = "investor@indexdesk.com.br", password = "SecurePassword123!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken");
        content.Should().Contain("refreshToken");
    }
}
