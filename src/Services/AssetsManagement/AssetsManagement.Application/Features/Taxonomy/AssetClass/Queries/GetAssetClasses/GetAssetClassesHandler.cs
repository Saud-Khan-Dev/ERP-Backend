using Microsoft.EntityFrameworkCore;

public class GetAssetClassesHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetClassesQuery, Result<GetAssetClassesQueryResult>>
{
  public async Task<Result<GetAssetClassesQueryResult>> Handle(GetAssetClassesQuery query, CancellationToken cancellationToken)
  {
    var classes = context.AssetClasses.AsNoTracking();

    if (!query.IncludeInactive)
      classes = classes.Where(c => c.IsActive);

    var data = await classes
      .OrderBy(c => c.DisplayOrder)
      .ThenBy(c => c.Code)
      .ToListAsync(cancellationToken);

    return Result<GetAssetClassesQueryResult>.Success(new GetAssetClassesQueryResult(data.Select(c => c.ToDto()).ToList()));
  }
}
