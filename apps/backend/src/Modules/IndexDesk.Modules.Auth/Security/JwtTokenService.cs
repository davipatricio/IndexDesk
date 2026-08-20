using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IndexDesk.Modules.Auth.Security;

public sealed class JwtTokenService : ITokenService
{
    private const string DefaultSecretKey =
        "development_super_secret_key_with_at_least_256_bits_for_indexdesk_jwt_signing_token_2026";
    private const string DefaultIssuer = "IndexDesk.Auth";
    private const string DefaultAudience = "IndexDesk.Web";
    private const int DefaultExpirationMinutes = 15;

    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(
        UserEntity user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        TimeSpan? lifetime = null
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        var secret =
            _configuration["Jwt:SecretKey"]
            ?? _configuration["Jwt:Secret"]
            ?? DefaultSecretKey;
        var issuer = _configuration["Jwt:Issuer"] ?? DefaultIssuer;
        var audience = _configuration["Jwt:Audience"] ?? DefaultAudience;

        var expirationMinutes = int.TryParse(
            _configuration["Jwt:AccessTokenExpirationMinutes"],
            out var mins
        )
            ? mins
            : DefaultExpirationMinutes;

        var tokenLifetime = lifetime ?? TimeSpan.FromMinutes(expirationMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (roles != null)
        {
            foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        if (permissions != null)
        {
            foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(permission))
                {
                    claims.Add(new Claim("permission", permission));
                }
            }
        }

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(tokenLifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Base64UrlEncoder.Encode(randomBytes);
    }

    public string ComputeTokenHash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(bytes);
    }
}
