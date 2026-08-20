using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace IndexDesk.Modules.Auth.Security;

public sealed class AuthCookieService : IAuthCookieService
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string AuthPath = "/api/v1/auth";

    private readonly IHostEnvironment? _environment;

    public AuthCookieService(IHostEnvironment? environment = null)
    {
        _environment = environment;
    }

    public void SetRefreshTokenCookie(HttpContext context, string token, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var isDevelopment = _environment?.IsDevelopment() ?? false;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps || !isDevelopment,
            SameSite = SameSiteMode.Lax,
            Path = AuthPath,
            Expires = new DateTimeOffset(expiresAt.ToUniversalTime()),
        };

        context.Response.Cookies.Append(RefreshTokenCookieName, token, options);
    }

    public void ClearRefreshTokenCookie(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = AuthPath,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            MaxAge = TimeSpan.Zero,
        };

        context.Response.Cookies.Delete(RefreshTokenCookieName, options);
    }
}
