namespace IndexDesk.Modules.Auth.Domain.Dtos;

public sealed record SignUpRequest(string Email, string Password, string FullName);

public sealed record SignInRequest(string Email, string Password);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions
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
