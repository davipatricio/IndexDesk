using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IndexDesk.Modules.Auth.Domain.Dtos;
using IndexDesk.Modules.Auth.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IndexDesk.Modules.Auth.Endpoints;

/// <summary>
/// User profile surface of the Auth module (distinct from the token routes of
/// <see cref="AuthEndpoints" />). Holds the preferences endpoints consumed by the
/// dashboard privacy toggle ("Esconder dados").
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group
            .MapPatch(
                "/me",
                async (
                    UpdateUserPreferencesRequest request,
                    IAuthService authService,
                    HttpContext httpContext,
                    CancellationToken ct
                ) =>
                {
                    var userIdClaim =
                        httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!Guid.TryParse(userIdClaim, out var userId))
                    {
                        return Results.Unauthorized();
                    }

                    var result = await authService.UpdatePreferencesAsync(userId, request, ct);
                    if (result.IsFailure)
                    {
                        return result.Error.Code == "Validation.MissingHideValues"
                            ? Results.BadRequest(new { message = result.Error.Message })
                            : Results.NotFound(result.Error.Message);
                    }

                    return Results.Ok(result.Value);
                }
            )
            .RequireAuthorization()
            .WithName("UpdateUserPreferences")
            .WithSummary("Update the authenticated user's preferences (privacy toggle)");

        return app;
    }
}
