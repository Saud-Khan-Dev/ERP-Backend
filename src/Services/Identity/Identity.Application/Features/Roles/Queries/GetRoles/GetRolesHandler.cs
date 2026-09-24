using Microsoft.EntityFrameworkCore;

public class GetRolesHandler(IApplicationDbContext context)
  : IQueryHandler<GetRolesQuery, Result<GetRolesQueryResult>>
{
  public async Task<Result<GetRolesQueryResult>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
  {
    var roles = context.Roles.AsNoTracking();

    if (!query.IncludeInactive)
      roles = roles.Where(r => r.IsActive);

    var data = await roles.OrderBy(r => r.Code).ToListAsync(cancellationToken);

    var counts = await context.RolePermissions.AsNoTracking()
        .GroupBy(rp => rp.RoleId)
        .Select(g => new { RoleId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

    return Result<GetRolesQueryResult>.Success(
      new GetRolesQueryResult(data.Select(r => r.ToDto(counts.GetValueOrDefault(r.Id, 0))).ToList()));
  }
}
