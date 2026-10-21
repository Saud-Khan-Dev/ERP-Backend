using Microsoft.EntityFrameworkCore;

/// The privilege-escalation guard rails that every administrative command shares. Power only ever flows downward:
///
/// - nobody edits their own authorization (roles, exceptions, active state, password from Administration);
/// - only a Super Administrator acts on a Super Administrator's account (reset, edit, unlock, deactivate, delete, sign
///   out, roles, exceptions) - otherwise "Reset password" on a Super Admin hands its password to a lesser account;
/// - nobody grants access they do not hold themselves - roles given to an account, permissions put on a role, Allow
///   exceptions (a Super Administrator excepted);
/// - nobody rewrites a role they hold themselves.
/// Together they stop an administrator of one area from promoting themselves through the normal API.
public class IdentityGuard(IApplicationDbContext context, ICurrentUser currentUser)
{
  /// The caller holds the Super Administrator role (from the token).
  public bool CallerIsSuperAdmin => currentUser.Roles.Contains(Role.SuperAdminCode, StringComparer.Ordinal);

  /// Only a Super Administrator acts on a Super Administrator's account.
  public async Task EnsureCanActOnAsync(UserId target, string action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(target);
    if (CallerIsSuperAdmin)
      return;
    if (await HoldsSuperAdminAsync(target, cancellationToken))
      throw new DomainException($"Only a Super Administrator can {action} a Super Administrator's account.");
  }

  /// Nobody hands out access they do not have themselves (a Super Administrator excepted).
  public async Task EnsureCanGrantAsync(IEnumerable<PermissionId> permissionIds, string what, CancellationToken cancellationToken)
  {
    if (CallerIsSuperAdmin)
      return;
    var ids = permissionIds.Distinct().ToList();
    if (ids.Count == 0)
      return;

    var wanted = await context.Permissions.AsNoTracking()
      .Where(p => ids.Contains(p.Id))
      .Select(p => new { p.Code, p.PermissionName })
      .ToListAsync(cancellationToken);
    var held = currentUser.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var missing = wanted.Where(p => !held.Contains(p.Code.Value)).Select(p => p.PermissionName.Value).OrderBy(n => n).ToList();
    if (missing.Count > 0)
      throw new DomainException($"You cannot {what}: it gives access you do not have yourself ({string.Join(", ", missing.Take(4))}{(missing.Count > 4 ? $" and {missing.Count - 4} more" : "")}).");
  }

  /// Giving roles is giving everything they carry.
  public async Task EnsureCanGrantRolesAsync(IEnumerable<RoleId> roleIds, string what, CancellationToken cancellationToken)
  {
    if (CallerIsSuperAdmin)
      return;
    var ids = roleIds.Distinct().ToList();
    if (ids.Count == 0)
      return;
    var permissionIds = await context.RolePermissions.AsNoTracking()
      .Where(rp => ids.Contains(rp.RoleId))
      .Select(rp => rp.PermissionId)
      .ToListAsync(cancellationToken);
    await EnsureCanGrantAsync(permissionIds, what, cancellationToken);
  }

  /// A role the caller holds is changed by another administrator - or they would be writing their own access.
  public async Task EnsureNotOwnRoleAsync(RoleId roleId, CancellationToken cancellationToken)
  {
    if (CallerIsSuperAdmin || currentUser.UserId is not { } me)
      return;
    var now = DateTime.UtcNow;
    var holds = await context.UserRoles.AsNoTracking()
      .AnyAsync(ur => ur.UserId == UserId.Of(me) && ur.RoleId == roleId && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now), cancellationToken);
    if (holds)
      throw new DomainException("You hold this role yourself - another administrator must change what it can do.");
  }

  /// The caller holds a permission (from the token) - for commands whose extra parts need more than the route asks.
  public void EnsurePermission(string permissionCode, string what)
  {
    if (!currentUser.HasPermission(permissionCode))
      throw new DomainException($"You cannot {what}.");
  }

  private async Task<bool> HoldsSuperAdminAsync(UserId userId, CancellationToken cancellationToken)
  {
    var superAdminCode = LookupCode.Of(Role.SuperAdminCode);
    var now = DateTime.UtcNow;
    return await context.UserRoles.AsNoTracking()
      .Where(ur => ur.UserId == userId && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now))
      .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => r)
      .AnyAsync(r => r.Code == superAdminCode, cancellationToken);
  }

  /// Blocks a Super Admin from changing their own roles, overrides or active status.
  /// Self-service actions (change password, list my sessions) go through the /auth endpoints instead.
  public void EnsureNotSelf(UserId targetUserId, string action)
  {
    ArgumentNullException.ThrowIfNull(targetUserId);

    if (currentUser.UserId == targetUserId.Value)
      throw new DomainException($"You cannot {action} your own account - another administrator must do it.");
  }

  /// Only an existing Super Admin may grant or remove the Super Admin role.
  public void EnsureCanAdministerRole(Role role)
  {
    ArgumentNullException.ThrowIfNull(role);

    if (role.IsSuperAdmin && !CallerIsSuperAdmin)
      throw new DomainException("Only a Super Administrator may give or take away the Super Administrator role.");
  }

  /// The system must always keep at least one usable Super Admin, or nobody can administer it again.
  public async Task EnsureNotLastSuperAdminAsync(UserId userId, string action, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(userId);

    var superAdminCode = LookupCode.Of(Role.SuperAdminCode);

    var superAdminRole = await context.Roles.AsNoTracking()
        .FirstOrDefaultAsync(r => r.Code == superAdminCode, cancellationToken);

    if (superAdminRole is null)
      return;

    var isSuperAdmin = await context.UserRoles.AsNoTracking()
        .AnyAsync(ur => ur.UserId == userId && ur.RoleId == superAdminRole.Id && ur.RevokedAt == null, cancellationToken);

    if (!isSuperAdmin)
      return;

    var now = DateTime.UtcNow;

    var otherSuperAdmins = await context.UserRoles.AsNoTracking()
        .Where(ur => ur.RoleId == superAdminRole.Id && ur.UserId != userId && ur.RevokedAt == null
                     && (ur.ExpiresAt == null || ur.ExpiresAt > now))
        .Join(context.Users.AsNoTracking(), ur => ur.UserId, u => u.Id, (ur, u) => u)
        .CountAsync(u => u.IsActive, cancellationToken);

    if (otherSuperAdmins == 0)
      throw new DomainException($"This is the last active Super Administrator; {action} would leave nobody able to manage the system.");
  }
}
