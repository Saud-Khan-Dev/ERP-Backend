using Microsoft.EntityFrameworkCore;

public class GetRolesHandler(IApplicationDbContext context)
  : IQueryHandler<GetRolesQuery, Result<GetRolesQueryResult>>
{
  public async Task<Result<GetRolesQueryResult>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var roles = context.Roles.AsNoTracking();

    if (!query.IncludeInactive)
      roles = roles.Where(r => r.IsActive);

    var data = await roles.OrderBy(r => r.RoleName).ToListAsync(cancellationToken);

    var counts = await context.RolePermissions.AsNoTracking()
        .GroupBy(rp => rp.RoleId)
        .Select(g => new { RoleId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

    // who holds it now - the same liveness as the Users register's role filter, on accounts that are not deleted
    var holders = await context.UserRoles.AsNoTracking()
        .Where(ur => ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now))
        .Join(context.Users.AsNoTracking(), ur => ur.UserId, u => u.Id, (ur, u) => ur.RoleId)
        .GroupBy(roleId => roleId)
        .Select(g => new { RoleId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

    return Result<GetRolesQueryResult>.Success(new GetRolesQueryResult(data
      .Select(r => r.ToDto(counts.GetValueOrDefault(r.Id, 0), holders.GetValueOrDefault(r.Id, 0)))
      .ToList()));
  }
}
