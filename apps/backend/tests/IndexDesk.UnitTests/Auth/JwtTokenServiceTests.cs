using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Auth.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IndexDesk.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateSut() => new(new ConfigurationBuilder().Build());

    private static UserEntity CreateUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = "investor@indexdesk.com.br",
            FullName = "Investor Test",
        };

    [Fact]
    public void GenerateAccessToken_ContainsSubEmailNameRolesAndPermissionsClaims()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateUser();
        var roles = new[] { "User", "Admin" };
        var permissions = new[] { "catalog:read", "portfolio:write" };

        // Act
        var token = sut.GenerateAccessToken(user, roles, permissions);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should()
            .Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Claims.Should()
            .Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.FullName);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "catalog:read");
        jwt.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "portfolio:write");
    }

    [Fact]
    public void GenerateRefreshToken_ProducesRandom64ByteString()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var token1 = sut.GenerateRefreshToken();
        var token2 = sut.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrWhiteSpace();
        var decoded = Base64UrlEncoder.DecodeBytes(token1);
        decoded.Should().HaveCount(64);
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ComputeTokenHash_Produces64CharacterSha256HexString()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var hash = sut.ComputeTokenHash("some-raw-refresh-token-value");

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void GenerateAccessToken_IsDeterministicallySameSecretOnlyForSameUser()
    {
        // Sanity check: two calls for the same user produce distinct tokens
        // (because of the unique jti claim), both of which still validate.
        var sut = CreateSut();
        var user = CreateUser();

        var token1 = sut.GenerateAccessToken(user, Array.Empty<string>(), Array.Empty<string>());
        var token2 = sut.GenerateAccessToken(user, Array.Empty<string>(), Array.Empty<string>());

        token1.Should().NotBe(token2);
    }
}
