using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Auth.Domain.Dtos;
using IndexDesk.Modules.Auth.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IndexDesk.Modules.Auth.Services;

public sealed class AuthService : IAuthService
{
    private const string DefaultUserRoleName = "User";
    private const int DefaultRefreshExpirationDays = 7;

    private readonly IndexDeskDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IndexDeskDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> SignUpAsync(
        SignUpRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    )
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!IsValidEmail(email))
        {
            return Result<AuthResponse>.Failure(
                Error.Validation("InvalidEmail", "A valid email address is required.")
            );
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return Result<AuthResponse>.Failure(
                Error.Validation("WeakPassword", "Password must be at least 8 characters long.")
            );
        }

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<AuthResponse>.Failure(
                Error.Validation("InvalidName", "Full name is required.")
            );
        }

        var existing = await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, ct);
        if (existing)
        {
            return Result<AuthResponse>.Failure(
                Error.Conflict("An account with this email already exists.")
            );
        }

        var defaultRole = await _db
            .Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == DefaultUserRoleName, ct);
        if (defaultRole is null)
        {
            return Result<AuthResponse>.Failure(
                Error.Failure("RoleNotFound", "Default role is not configured.")
            );
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = new UserEntity
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            IsActive = true,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        _db.UserRoles.Add(
            new UserRoleEntity
            {
                User = user,
                RoleId = defaultRole.Id,
                AssignedAt = DateTime.UtcNow,
            }
        );

        var (accessToken, refreshToken, expiresIn) = await IssueTokensAsync(user, ct);

        _db.RefreshTokens.Add(
            CreateRefreshTokenEntity(user.Id, refreshToken, ipAddress, userAgent)
        );

        await _db.SaveChangesAsync(ct);

        var dto = await GetUserDtoAsync(user.Id, ct);
        return Result<AuthResponse>.Success(
            new AuthResponse(accessToken, refreshToken, expiresIn, dto)
        );
    }

    public async Task<Result<AuthResponse>> SignInAsync(
        SignInRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    )
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var user = await LoadUserWithAccessAsync(email, ct);
        if (
            user is null
            || !user.IsActive
            || !_passwordHasher.VerifyPassword(request.Password ?? string.Empty, user.PasswordHash)
        )
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("Invalid email or password."));
        }

        user.LastLoginAt = DateTime.UtcNow;

        var (accessToken, refreshToken, expiresIn) = await IssueTokensAsync(user, ct);

        _db.RefreshTokens.Add(
            CreateRefreshTokenEntity(user.Id, refreshToken, ipAddress, userAgent)
        );

        await _db.SaveChangesAsync(ct);

        var dto = GetUserDto(user);
        return Result<AuthResponse>.Success(
            new AuthResponse(accessToken, refreshToken, expiresIn, dto)
        );
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("A refresh token is required."));
        }

        var tokenHash = _tokenService.ComputeTokenHash(rawRefreshToken);
        var stored = await _db
            .RefreshTokens.Include(r => r.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (stored is null)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("Invalid refresh token."));
        }

        if (stored.RevokedAt is not null)
        {
            // Token reuse detected: an attacker (or a replayed request) presented an
            // already-rotated token. Invalidate every active session for this user.
            var userTokens = _db.RefreshTokens.Where(r =>
                r.UserId == stored.UserId && r.RevokedAt == null
            );
            foreach (var t in userTokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(
                    "Refresh token reuse detected. All active sessions were revoked."
                )
            );
        }

        if (DateTime.UtcNow >= stored.ExpiresAt)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("Refresh token has expired."));
        }

        var user = stored.User;
        if (!user.IsActive)
        {
            return Result<AuthResponse>.Failure(Error.Unauthorized("Account is disabled."));
        }

        var (accessToken, newRefreshToken, expiresIn) = await IssueTokensAsync(user, ct);

        var newTokenHash = _tokenService.ComputeTokenHash(newRefreshToken);

        // Rotate: revoke the old token, point it at the replacement.
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newTokenHash;

        _db.RefreshTokens.Add(
            CreateRefreshTokenEntity(user.Id, newRefreshToken, ipAddress, userAgent)
        );

        await _db.SaveChangesAsync(ct);

        var dto = GetUserDto(user);
        return Result<AuthResponse>.Success(
            new AuthResponse(accessToken, newRefreshToken, expiresIn, dto)
        );
    }

    public async Task<Result> SignOutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result.Success();
        }

        var tokenHash = _tokenService.ComputeTokenHash(rawRefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var dto = await GetUserDtoAsync(userId, ct);
        return dto is null
            ? Result<UserDto>.Failure(Error.NotFound("User", userId.ToString()))
            : Result<UserDto>.Success(dto);
    }

    private async Task<(string accessToken, string refreshToken, int expiresIn)> IssueTokensAsync(
        UserEntity user,
        CancellationToken ct
    )
    {
        // Navigation may not be loaded yet (e.g. a freshly created sign-up user has
        // a UserRole with only RoleId set). Filter nulls so the reload fallback below
        // decides whether to load roles/permissions from the database.
        var roles =
            user.UserRoles?.Where(ur => ur.Role is not null).Select(ur => ur.Role.Name).ToList()
            ?? new List<string>();
        var permissions =
            user.UserRoles?.Where(ur => ur.Role is not null)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Where(rp => rp.Permission is not null)
                .Select(rp => rp.Permission.Slug)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            ?? new List<string>();

        // Ensure permissions are loaded for sign-up (user is not yet tracked with navigation).
        if (roles.Count == 0)
        {
            (roles, permissions) = await LoadRolesAndPermissionsAsync(user.Id, ct);
        }

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);

        var expiresIn = int.TryParse(
            _configuration["Jwt:AccessTokenExpirationMinutes"],
            out var mins
        )
            ? mins
            : 15;

        return (accessToken, _tokenService.GenerateRefreshToken(), expiresIn);
    }

    private async Task<(List<string> roles, List<string> permissions)> LoadRolesAndPermissionsAsync(
        Guid userId,
        CancellationToken ct
    )
    {
        var user = await LoadUserWithAccessAsync(userId.ToString(), ct, byId: true);
        if (user is null)
        {
            return (new List<string>(), new List<string>());
        }

        return (
            user.UserRoles.Select(ur => ur.Role.Name).ToList(),
            user.UserRoles.SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Slug)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        );
    }

    private async Task<UserDto?> GetUserDtoAsync(Guid userId, CancellationToken ct)
    {
        var user = await LoadUserWithAccessByGuidAsync(userId, ct);
        return user is null ? null : GetUserDto(user);
    }

    private async Task<UserEntity?> LoadUserWithAccessByGuidAsync(Guid userId, CancellationToken ct)
    {
        return await _db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    private static UserDto GetUserDto(UserEntity user)
    {
        var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
        var permissions =
            user.UserRoles?.SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Slug)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            ?? new List<string>();

        return new UserDto(user.Id, user.Email, user.FullName, roles, permissions);
    }

    private async Task<UserEntity?> LoadUserWithAccessAsync(
        string emailOrId,
        CancellationToken ct,
        bool byId = false
    )
    {
        IQueryable<UserEntity> query = _db
            .Users.Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission);

        if (byId)
        {
            var normalizedId = emailOrId.ToLowerInvariant();
            return await query.FirstOrDefaultAsync(u => u.Id == Guid.Parse(normalizedId), ct);
        }

        return await query.FirstOrDefaultAsync(u => u.Email == emailOrId.ToLowerInvariant(), ct);
    }

    private RefreshTokenEntity CreateRefreshTokenEntity(
        Guid userId,
        string rawToken,
        string? ipAddress,
        string? userAgent
    )
    {
        var expirationDays = int.TryParse(
            _configuration["Jwt:RefreshTokenExpirationDays"],
            out var days
        )
            ? days
            : DefaultRefreshExpirationDays;

        return new RefreshTokenEntity
        {
            UserId = userId,
            TokenHash = _tokenService.ComputeTokenHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
