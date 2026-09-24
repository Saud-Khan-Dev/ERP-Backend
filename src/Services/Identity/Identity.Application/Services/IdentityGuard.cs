using Microsoft.EntityFrameworkCore;

/// The privilege-escalation guard rails that every administrative command shares.
///
/// Holding IAM_USERS.ASSIGN is not by itself enough to hand out SUPER_ADMIN, and no administrator
/// may quietly edit their own authorization — those two rules are what stop an ordinary employee
/// from promoting themselves through the normal role-management API.
public class IdentityGuard(IApplicationDbContext context, ICurrentUser currentUser)
{
  /// Blocks a Super Admin from changing their own roles, overrides or active status.
  /// Self-service actions (change password, list my sessions) go through the /auth endpoints instead.
  public void EnsureNotSelf(UserId targetUserId, string action)
  {
    ArgumentNullException.ThrowIfNull(targetUserId);

    if (currentUser.UserId == targetUserId.Value)
      throw new DomainException($"You cannot {action} on your own account.");
  }

  /// Only an existing Super Admin may grant or remove the Super Admin role.
  public void EnsureCanAdministerRole(Role role)
  {
    ArgumentNullException.ThrowIfNull(role);

    if (role.IsSuperAdmin && !currentUser.Roles.Contains(Role.SuperAdminCode, StringComparer.Ordinal))
      throw new DomainException($"Only a {Role.SuperAdminCode} may grant or remove the {Role.SuperAdminCode} role.");
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
      throw new DomainException($"This is the last active {Role.SuperAdminCode}; {action} would leave the system unmanageable.");
  }
}
