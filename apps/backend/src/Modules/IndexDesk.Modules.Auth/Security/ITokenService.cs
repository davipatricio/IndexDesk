using IndexDesk.BuildingBlocks.Persistence.Entities;

namespace IndexDesk.Modules.Auth.Security;

public interface ITokenService
{
    string GenerateAccessToken(
        UserEntity user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        TimeSpan? lifetime = null
    );

    string GenerateRefreshToken();

    string ComputeTokenHash(string rawToken);
}
