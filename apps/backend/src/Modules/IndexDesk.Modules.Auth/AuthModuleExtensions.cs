using System.Text;
using IndexDesk.Modules.Auth.Domain;
using IndexDesk.Modules.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IndexDesk.Modules.Auth;

public static class AuthModuleExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var secret =
            configuration["Jwt:Secret"]
            ?? "development_super_secret_key_with_at_least_256_bits_for_indexdesk_jwt_signing_token_2026";
        var issuer = configuration["Jwt:Issuer"] ?? "IndexDesk.Auth";
        var audience = configuration["Jwt:Audience"] ?? "IndexDesk.Web";

        services.AddScoped<ITokenService, TokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.Zero,
                };
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                "AdminOnly",
                policy =>
                    policy.RequireRole(UserRole.Admin.ToString(), UserRole.SuperAdmin.ToString())
            )
            .AddPolicy(
                "SuperAdminOnly",
                policy => policy.RequireRole(UserRole.SuperAdmin.ToString())
            );

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group
            .MapPost(
                "/login",
                (LoginRequest request, ITokenService tokenService) =>
                {
                    // Scaffold demo validation
                    if (
                        string.IsNullOrWhiteSpace(request.Email)
                        || string.IsNullOrWhiteSpace(request.Password)
                    )
                        return Results.BadRequest(
                            new { message = "Email and password are required." }
                        );

                    var mockUser = new User
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                        Email = request.Email,
                        FullName = "Demo User",
                        Role = request.Email.Contains("admin") ? UserRole.Admin : UserRole.User,
                    };

                    var accessToken = tokenService.GenerateAccessToken(mockUser);
                    var refreshToken = tokenService.GenerateRefreshToken();

                    return Results.Ok(
                        new LoginResponse(accessToken, refreshToken, mockUser.Role.ToString())
                    );
                }
            )
            .WithName("Login")
            .WithSummary("User authentication endpoint returning JWT access token");

        group
            .MapGet(
                "/me",
                (HttpContext context) =>
                {
                    var user = context.User;
                    if (user.Identity?.IsAuthenticated != true)
                        return Results.Unauthorized();

                    return Results.Ok(
                        new
                        {
                            Email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                            Name = user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
                            Role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                        }
                    );
                }
            )
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get authenticated user claims");

        return app;
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, string RefreshToken, string Role);
