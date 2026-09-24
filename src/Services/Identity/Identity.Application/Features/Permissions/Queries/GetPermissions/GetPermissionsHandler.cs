using Microsoft.EntityFrameworkCore;

public class GetPermissionsHandler(IApplicationDbContext context)
  : IQueryHandler<GetPermissionsQuery, Result<GetPermissionsQueryResult>>
{
  public async Task<Result<GetPermissionsQueryResult>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
  {
    var permissions = context.Permissions.AsNoTracking()
        .Join(context.PermissionModules.AsNoTracking(), p => p.ModuleId, m => m.Id,
            (p, m) => new { Permission = p, Module = m });

    if (!string.IsNullOrWhiteSpace(query.Module))
    {
      var module = LookupCode.Of(query.Module);
      permissions = permissions.Where(x => x.Module.Code == module);
    }

    if (!query.IncludeInactive)
      permissions = permissions.Where(x => x.Permission.IsActive);

    var data = await permissions
        .OrderBy(x => x.Module.Code)
        .ThenBy(x => x.Permission.Code)
        .ToListAsync(cancellationToken);

    return Result<GetPermissionsQueryResult>.Success(
      new GetPermissionsQueryResult(data.Select(x => x.Permission.ToDto(x.Module.Code.Value)).ToList()));
  }
}
