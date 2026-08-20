using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.Auth.Domain.Dtos;

namespace IndexDesk.Modules.Auth.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> SignUpAsync(
        SignUpRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    );

    Task<Result<AuthResponse>> SignInAsync(
        SignInRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    );

    Task<Result<AuthResponse>> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    );

    Task<Result> SignOutAsync(string rawRefreshToken, CancellationToken ct = default);

    Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
