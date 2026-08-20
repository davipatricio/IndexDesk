using System.Security.Claims;
using FluentAssertions;
using IndexDesk.Modules.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace IndexDesk.UnitTests.Auth;

public class PermissionAuthorizationHandlerTests
{
    private static ClaimsPrincipal CreateUser(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "TestAuth"
        );
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_Succeeds_WhenUserHasMatchingPermissionClaim()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("portfolio:write");
        var user = CreateUser(("permission", "catalog:read"), ("permission", "portfolio:write"));
        var context = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { requirement },
            user,
            resource: null
        );

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Fails_WhenUserLacksMatchingPermissionClaim()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("admin:access");
        var user = CreateUser(("permission", "catalog:read"));
        var context = new AuthorizationHandlerContext(
            new IAuthorizationRequirement[] { requirement },
            user,
            resource: null
        );

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }
}
