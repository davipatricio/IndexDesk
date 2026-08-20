using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace IndexDesk.IntegrationTests.Auth;

/// <summary>
/// Integration tests for the /api/v1/auth endpoint group.
/// Uses a dedicated PostgreSQL database via <see cref="AuthWebApplicationFactory"/>.
/// </summary>
public class AuthEndpointsTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public AuthEndpointsTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string NewEmail() => $"user_{Guid.NewGuid():N}@indexdesk.com.br";

    [Fact]
    public async Task SignUp_CreatesUser_Returns201CreatedWithJwtAndSetCookie()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new
        {
            email = NewEmail(),
            password = "SecurePassword123!",
            fullName = "Test User",
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/signup", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Should().ContainKey("Set-Cookie");

        var body = await response.Content.ReadFromJsonAsync<TestAuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.ExpiresIn.Should().BeGreaterThan(0);
        body.User.Email.Should().Be(payload.email);
        body.User.FullName.Should().Be("Test User");
        body.User.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task SignIn_WithValidCredentials_Returns200OkWithJwt()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = NewEmail();
        var password = "SecurePassword123!";

        var signUp = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new
            {
                email,
                password,
                fullName = "Sign In User",
            }
        );
        signUp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/signin", new { email, password });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TestAuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task SignIn_WithInvalidCredentials_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/signin",
            new { email = "ghost@indexdesk.com.br", password = "WrongPassword123!" }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithBearerToken_ReturnsUserDto()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = NewEmail();

        var signUp = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new
            {
                email,
                password = "SecurePassword123!",
                fullName = "Me User",
            }
        );
        var signUpBody = await signUp.Content.ReadFromJsonAsync<TestAuthResponse>();
        signUpBody.Should().NotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            signUpBody!.AccessToken
        );

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<TestUserDto>();
        me.Should().NotBeNull();
        me!.Id.Should().Be(signUpBody.User.Id);
        me.Email.Should().Be(email);
        me.FullName.Should().Be("Me User");
        me.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_RevokesSessionAndClearsCookie()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = NewEmail();

        var signUp = await client.PostAsJsonAsync(
            "/api/v1/auth/signup",
            new
            {
                email,
                password = "SecurePassword123!",
                fullName = "Sign Out User",
            }
        );
        signUp.StatusCode.Should().Be(HttpStatusCode.Created);

        var setCookie = signUp.Headers.GetValues("Set-Cookie").First();
        var cookieHeader = setCookie.Split(';', 2)[0]; // refreshToken=<value>

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/signout");
        request.Headers.Add("Cookie", cookieHeader);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Should().ContainKey("Set-Cookie");

        var clearCookie = response.Headers.GetValues("Set-Cookie").First();
        clearCookie.Should().Contain("refreshToken=;");
        clearCookie.ToLowerInvariant().Should().Contain("expires=");
    }

    // Mirrors the wire contract without coupling the test to module internals.
    private sealed record TestUserDto(
        Guid Id,
        string Email,
        string FullName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions
    );

    private sealed record TestAuthResponse(string AccessToken, int ExpiresIn, TestUserDto User);
}
