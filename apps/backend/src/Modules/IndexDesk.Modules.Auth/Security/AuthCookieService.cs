using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IndexDesk.Modules.Auth.Security;

public sealed class AuthCookieService : IAuthCookieService
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string AuthPath = "/api/v1/auth";

    private readonly IHostEnvironment? _environment;
    private readonly bool _crossSite;

    /// <summary>
    /// `_crossSite` (config `Auth:CookieCrossSite`) habilita SameSite=None + Secure para cenários
    /// em que a API e o frontend vivem em origens diferentes (ex.: túneis HTTPS separados).
    /// Sem ele, o cookie de refresh não viaja no fetch cross-site e o refresh silencioso quebra.
    /// </summary>
    public AuthCookieService(IHostEnvironment? environment = null, IConfiguration? configuration = null)
    {
        _environment = environment;
        _crossSite =
            configuration?.GetValue<bool>("Auth:CookieCrossSite")
            ?? bool.TryParse(Environment.GetEnvironmentVariable("Auth__CookieCrossSite"), out var env)
                && env;
    }

    public void SetRefreshTokenCookie(HttpContext context, string token, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var isDevelopment = _environment?.IsDevelopment() ?? false;

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = _crossSite || context.Request.IsHttps || !isDevelopment,
            SameSite = _crossSite ? SameSiteMode.None : SameSiteMode.Lax,
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
            Secure = _crossSite || context.Request.IsHttps || !(_environment?.IsDevelopment() ?? false),
            SameSite = _crossSite ? SameSiteMode.None : SameSiteMode.Lax,
            Path = AuthPath,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            MaxAge = TimeSpan.Zero,
        };

        context.Response.Cookies.Delete(RefreshTokenCookieName, options);
    }
}
