using Microsoft.EntityFrameworkCore;

public class UserPermissionService(IApplicationDbContext context) : IUserPermissionService
{
  public async Task<PermissionResolver.ResolvedPermissions> ResolveAsync(UserId userId, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(userId);

    var now = DateTime.UtcNow;

    // role grants, joined to the roles themselves
    var roleGrants = await context.UserRoles
        .AsNoTracking()
        .Where(ur => ur.UserId == userId)
        .Join(context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id,
            (ur, r) => new PermissionResolver.RoleGrant(r, ur))
        .ToListAsync(cancellationToken);

    var liveRoleIds = roleGrants
        .Where(g => g.Assignment.IsLive(now) && g.Role.IsActive && !g.Role.IsDeleted)
        .Select(g => g.Role.Id)
        .Distinct()
        .ToList();

    var rolePermissions = liveRoleIds.Count == 0
        ? new List<PermissionResolver.RolePermissionGrant>()
        : await context.RolePermissions
            .AsNoTracking()
            .Where(rp => liveRoleIds.Contains(rp.RoleId))
            .Join(context.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id,
                (rp, p) => new PermissionResolver.RolePermissionGrant(rp.RoleId, p))
            .ToListAsync(cancellationToken);

    var overrides = await context.UserPermissionOverrides
        .AsNoTracking()
        .Where(o => o.UserId == userId)
        .Join(context.Permissions.AsNoTracking(), o => o.PermissionId, p => p.Id,
            (o, p) => new PermissionResolver.OverrideGrant(o, p))
        .ToListAsync(cancellationToken);

    return PermissionResolver.Resolve(roleGrants, rolePermissions, overrides, now);
  }
}
