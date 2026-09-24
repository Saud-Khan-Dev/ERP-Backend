using Microsoft.EntityFrameworkCore;

public class GetPermissionModulesHandler(IApplicationDbContext context)
  : IQueryHandler<GetPermissionModulesQuery, Result<GetPermissionModulesQueryResult>>
{
  public async Task<Result<GetPermissionModulesQueryResult>> Handle(GetPermissionModulesQuery query, CancellationToken cancellationToken)
  {
    var modules = await context.PermissionModules.AsNoTracking()
        .OrderBy(m => m.Code)
        .ToListAsync(cancellationToken);

    return Result<GetPermissionModulesQueryResult>.Success(
      new GetPermissionModulesQueryResult(modules.Select(m => m.ToDto()).ToList()));
  }
}
