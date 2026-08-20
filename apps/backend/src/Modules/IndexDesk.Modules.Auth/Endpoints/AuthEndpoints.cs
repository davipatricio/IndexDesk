using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.Auth.Domain.Dtos;
using IndexDesk.Modules.Auth.Security;
using IndexDesk.Modules.Auth.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace IndexDesk.Modules.Auth.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group
            .MapPost(
                "/signup",
                async (
                    SignUpRequest request,
                    IAuthService authService,
                    IAuthCookieService cookieService,
                    IConfiguration configuration,
                    HttpContext httpContext,
                    CancellationToken ct
                ) =>
                {
                    var result = await authService.SignUpAsync(
                        request,
                        httpContext.Connection.RemoteIpAddress?.ToString(),
                        httpContext.Request.Headers.UserAgent.ToString(),
                        ct
                    );

                    if (result.IsFailure)
                    {
                        return ErrorToResult(result.Error);
                    }

                    cookieService.SetRefreshTokenCookie(
                        httpContext,
                        result.Value.RefreshToken,
                        RefreshExpiry(configuration)
                    );

                    return Results.Created("/api/v1/auth/me", result.Value);
                }
            )
            .WithName("SignUp")
            .WithSummary("Register a new user account and issue an access token");

        group
            .MapPost(
                "/signin",
                async (
                    SignInRequest request,
                    IAuthService authService,
                    IAuthCookieService cookieService,
                    IConfiguration configuration,
                    HttpContext httpContext,
                    CancellationToken ct
                ) =>
                {
                    var result = await authService.SignInAsync(
                        request,
                        httpContext.Connection.RemoteIpAddress?.ToString(),
                        httpContext.Request.Headers.UserAgent.ToString(),
                        ct
                    );

                    if (result.IsFailure)
                    {
                        return ErrorToResult(result.Error);
                    }

                    cookieService.SetRefreshTokenCookie(
                        httpContext,
                        result.Value.RefreshToken,
                        RefreshExpiry(configuration)
                    );
                    return Results.Ok(result.Value);
                }
            )
            .WithName("SignIn")
            .WithSummary("Authenticate with email and password and issue an access token");

        group
            .MapPost(
                "/refresh",
                async (
                    IAuthService authService,
                    IAuthCookieService cookieService,
                    IConfiguration configuration,
                    HttpContext httpContext,
                    CancellationToken ct
                ) =>
                {
                    var rawToken = httpContext.Request.Cookies[
                        AuthCookieService.RefreshTokenCookieName
                    ];

                    var result = await authService.RefreshTokenAsync(
                        rawToken ?? string.Empty,
                        httpContext.Connection.RemoteIpAddress?.ToString(),
                        httpContext.Request.Headers.UserAgent.ToString(),
                        ct
                    );

                    if (result.IsFailure)
                    {
                        return ErrorToResult(result.Error);
                    }

                    cookieService.SetRefreshTokenCookie(
                        httpContext,
                        result.Value.RefreshToken,
                        RefreshExpiry(configuration)
                    );
                    return Results.Ok(result.Value);
                }
            )
            .WithName("RefreshToken")
            .WithSummary(
                "Rotate the refresh token from the HttpOnly cookie and issue a new access token"
            );

        group
            .MapPost(
                "/signout",
                async (
                    IAuthService authService,
                    IAuthCookieService cookieService,
                    HttpContext httpContext,
                    CancellationToken ct
                ) =>
                {
                    var rawToken = httpContext.Request.Cookies[
                        AuthCookieService.RefreshTokenCookieName
                    ];

                    await authService.SignOutAsync(rawToken ?? string.Empty, ct);
                    cookieService.ClearRefreshTokenCookie(httpContext);

                    return Results.NoContent();
                }
            )
            .WithName("SignOut")
            .WithSummary("Revoke the current refresh token and clear the HttpOnly cookie");

        group
            .MapGet(
                "/me",
                async (IAuthService authService, HttpContext httpContext, CancellationToken ct) =>
                {
                    var userIdClaim =
                        httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!Guid.TryParse(userIdClaim, out var userId))
                    {
                        return Results.Unauthorized();
                    }

                    var result = await authService.GetCurrentUserAsync(userId, ct);
                    return result.IsSuccess
                        ? Results.Ok(result.Value)
                        : Results.NotFound(result.Error.Message);
                }
            )
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user's profile with roles and permissions");

        return app;
    }

    private static DateTime RefreshExpiry(IConfiguration configuration)
    {
        var days = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var parsed)
            ? parsed
            : 7;

        return DateTime.UtcNow.AddDays(days);
    }

    private static IResult ErrorToResult(Error error)
    {
        return error.Code switch
        {
            "Auth.Unauthorized" => Results.Unauthorized(),
            "Conflict" => Results.Conflict(new { message = error.Message }),
            _ => Results.BadRequest(new { message = error.Message }),
        };
    }
}
