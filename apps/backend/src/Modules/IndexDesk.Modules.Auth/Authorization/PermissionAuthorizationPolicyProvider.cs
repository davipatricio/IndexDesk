using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace IndexDesk.Modules.Auth.Authorization;

/// <summary>
/// Resolves authorization policies on demand. Policies prefixed with <see cref="HasPermissionAttribute.PolicyPrefix"/>
/// (e.g. "PERMISSION:portfolio:write") are translated into a <see cref="PermissionRequirement"/>;
/// every other policy is delegated to the default provider.
/// </summary>
public sealed class PermissionAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options
    )
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (
            policyName.StartsWith(
                HasPermissionAttribute.PolicyPrefix,
                StringComparison.Ordinal
            )
        )
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
