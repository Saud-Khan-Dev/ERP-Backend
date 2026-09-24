/// Turns a user's role grants and personal overrides into the flat set of permission codes that go
/// into their access token.
///
/// Precedence — an explicit DENY always wins:
///
///     1. override DENY  (live)          -> denied, whatever the roles say
///     2. override ALLOW (live)          -> allowed
///     3. any live role grants it        -> allowed
///     4. otherwise                      -> denied
///
/// Expired or revoked grants, inactive roles and inactive permissions contribute nothing.
public static class PermissionResolver
{
  /// A live role grant, already joined to its role.
  public sealed record RoleGrant(Role Role, UserRole Assignment);

  /// A permission attached to a role.
  public sealed record RolePermissionGrant(RoleId RoleId, Permission Permission);

  /// A personal override, already joined to its permission.
  public sealed record OverrideGrant(UserPermissionOverride Override, Permission Permission);

  public sealed record ResolvedPermissions(
      IReadOnlySet<string> PermissionCodes,
      IReadOnlySet<string> RoleCodes,
      IReadOnlySet<string> DeniedCodes);

  public static ResolvedPermissions Resolve(
      IEnumerable<RoleGrant> roleGrants,
      IEnumerable<RolePermissionGrant> rolePermissions,
      IEnumerable<OverrideGrant> overrides,
      DateTime now)
  {
    ArgumentNullException.ThrowIfNull(roleGrants);
    ArgumentNullException.ThrowIfNull(rolePermissions);
    ArgumentNullException.ThrowIfNull(overrides);

    var liveRoles = roleGrants
        .Where(g => g.Assignment.IsLive(now) && g.Role.IsActive && !g.Role.IsDeleted)
        .Select(g => g.Role)
        .ToList();

    var liveRoleIds = liveRoles.Select(r => r.Id).ToHashSet();

    var granted = rolePermissions
        .Where(rp => liveRoleIds.Contains(rp.RoleId) && rp.Permission.IsActive)
        .Select(rp => rp.Permission.Code.Value)
        .ToHashSet(StringComparer.Ordinal);

    var liveOverrides = overrides
        .Where(o => o.Override.IsLive(now) && o.Permission.IsActive)
        .ToList();

    foreach (var allow in liveOverrides.Where(o => o.Override.Effect == OverrideEffect.Allow))
      granted.Add(allow.Permission.Code.Value);

    // DENY last, so it beats both role grants and ALLOW overrides
    var denied = liveOverrides
        .Where(o => o.Override.Effect == OverrideEffect.Deny)
        .Select(o => o.Permission.Code.Value)
        .ToHashSet(StringComparer.Ordinal);

    granted.ExceptWith(denied);

    return new ResolvedPermissions(
        granted,
        liveRoles.Select(r => r.Code.Value).ToHashSet(StringComparer.Ordinal),
        denied);
  }
}
