namespace IndexDesk.Modules.Auth.Domain.Dtos;

public sealed record SignUpRequest(string Email, string Password, string FullName);

public sealed record SignInRequest(string Email, string Password);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    UserPreferencesDto Preferences
);

/// <summary>
/// Auth response sent to the client. The access token is returned in the body;
/// the refresh token is echoed here so the endpoint can also persist it as an
/// HttpOnly cookie (and the /refresh flow can rotate it).
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    UserDto User
);

/// <summary>UI preferences returned by /me and PATCH /users/me (privacy toggle).</summary>
public sealed record UserPreferencesDto(bool HideValues);

/// <summary>
/// PATCH /api/v1/users/me payload. A <see langword="null" /> HideValues means "leave
/// unchanged" (absent key); the endpoint rejects a request without any field set.
/// </summary>
public sealed record UpdateUserPreferencesRequest(bool? HideValues);
