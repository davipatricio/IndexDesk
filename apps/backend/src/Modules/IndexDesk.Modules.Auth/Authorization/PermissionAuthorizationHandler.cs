using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace IndexDesk.Modules.Auth.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement
    )
    {
        if (
            context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim("permission", requirement.Permission)
        )
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
