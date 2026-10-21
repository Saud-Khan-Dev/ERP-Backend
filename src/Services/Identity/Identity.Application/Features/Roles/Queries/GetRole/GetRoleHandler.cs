using Microsoft.EntityFrameworkCore;

public class GetRoleHandler(IApplicationDbContext context)
  : IQueryHandler<GetRoleQuery, Result<GetRoleQueryResult>>
{
  public async Task<Result<GetRoleQueryResult>> Handle(GetRoleQuery query, CancellationToken cancellationToken)
  {
    var id = RoleId.Of(query.Id);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new RoleNotFoundException("This role was not found - it may have been deleted.");

    var permissions = await context.RolePermissions.AsNoTracking()
        .Where(rp => rp.RoleId == id)
        .Join(context.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id, (rp, p) => p)
        .Join(context.PermissionModules.AsNoTracking(), p => p.ModuleId, m => m.Id,
            (p, m) => new { Permission = p, Module = m })
        .OrderBy(x => x.Permission.Code)
        .ToListAsync(cancellationToken);

    var now = DateTime.UtcNow;
    var holders = await context.UserRoles.AsNoTracking()
        .Where(ur => ur.RoleId == id && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now))
        .Join(context.Users.AsNoTracking(), ur => ur.UserId, u => u.Id, (ur, u) => ur.Id)
        .CountAsync(cancellationToken);

    return Result<GetRoleQueryResult>.Success(new GetRoleQueryResult(
      role.ToDto(permissions.Count, holders),
      permissions.Select(x => x.Permission.ToDto(x.Module.Code.Value)).ToList()));
  }
}
