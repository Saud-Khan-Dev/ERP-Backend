using Microsoft.EntityFrameworkCore;

public class GetRoleHandler(IApplicationDbContext context)
  : IQueryHandler<GetRoleQuery, Result<GetRoleQueryResult>>
{
  public async Task<Result<GetRoleQueryResult>> Handle(GetRoleQuery query, CancellationToken cancellationToken)
  {
    var id = RoleId.Of(query.Id);

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {query.Id} was not found.");

    var permissions = await context.RolePermissions.AsNoTracking()
        .Where(rp => rp.RoleId == id)
        .Join(context.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id, (rp, p) => p)
        .Join(context.PermissionModules.AsNoTracking(), p => p.ModuleId, m => m.Id,
            (p, m) => new { Permission = p, Module = m })
        .OrderBy(x => x.Permission.Code)
        .ToListAsync(cancellationToken);

    return Result<GetRoleQueryResult>.Success(new GetRoleQueryResult(
      role.ToDto(permissions.Count),
      permissions.Select(x => x.Permission.ToDto(x.Module.Code.Value)).ToList()));
  }
}
