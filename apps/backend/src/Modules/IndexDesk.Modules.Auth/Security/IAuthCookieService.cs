using Microsoft.AspNetCore.Http;

namespace IndexDesk.Modules.Auth.Security;

public interface IAuthCookieService
{
    void SetRefreshTokenCookie(HttpContext context, string token, DateTime expiresAt);
    void ClearRefreshTokenCookie(HttpContext context);
}
