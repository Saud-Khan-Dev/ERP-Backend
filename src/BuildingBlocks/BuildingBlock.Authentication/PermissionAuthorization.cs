using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

/// Requires one `MODULE.ACTION` permission claim on the caller.
public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
  public string PermissionCode { get; } = permissionCode;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
  protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
  {
    if (context.User.HasClaim(ErpClaimTypes.Permission, requirement.PermissionCode))
      context.Succeed(requirement);

    return Task.CompletedTask;
  }
}

/// Creates a policy on demand for any "perm:CODE" policy name, so services never have to register
/// one policy per permission. Anything else falls through to the default provider.
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
  public const string PolicyPrefix = "perm:";

  private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

  public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

  public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

  public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
  {
    if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
      return _fallback.GetPolicyAsync(policyName);

    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new PermissionRequirement(policyName[PolicyPrefix.Length..]))
        .Build();

    return Task.FromResult<AuthorizationPolicy?>(policy);
  }
}
