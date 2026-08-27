using System.Text;
using IndexDesk.Modules.Auth.Authorization;
using IndexDesk.Modules.Auth.Endpoints;
using IndexDesk.Modules.Auth.Security;
using IndexDesk.Modules.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace IndexDesk.Modules.Auth;

public static class AuthModuleExtensions
{
    private const string DefaultSecretKey =
        "development_super_secret_key_with_at_least_256_bits_for_indexdesk_jwt_signing_token_2026";
    private const string DefaultIssuer = "IndexDesk.Auth";
    private const string DefaultAudience = "IndexDesk.Web";

    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var secret =
            configuration["Jwt:SecretKey"] ?? configuration["Jwt:Secret"] ?? DefaultSecretKey;
        var issuer = configuration["Jwt:Issuer"] ?? DefaultIssuer;
        var audience = configuration["Jwt:Audience"] ?? DefaultAudience;

        // Security services & token management
        // Argon2idPasswordHasher and JwtTokenService are stateless -> safe as singletons.
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthCookieService, AuthCookieService>();

        // Auth application service
        services.AddScoped<IAuthService, AuthService>();

        // RBAC authorization engine
        services.AddSingleton<
            IAuthorizationPolicyProvider,
            PermissionAuthorizationPolicyProvider
        >();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

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

        // Registers core authorization services (IAuthorizationService,
        // policy evaluator) required by app.UseAuthorization(), plus the
        // role-based fallback policies.
        services
            .AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "SuperAdmin"))
            .AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        AuthEndpoints.MapAuthEndpoints(app);
        return UserEndpoints.MapUserEndpoints(app);
    }
}
