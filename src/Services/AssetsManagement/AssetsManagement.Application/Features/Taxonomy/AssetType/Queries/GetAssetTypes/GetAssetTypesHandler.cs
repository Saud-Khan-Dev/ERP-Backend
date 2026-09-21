using Microsoft.EntityFrameworkCore;

public class GetAssetTypesHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetTypesQuery, Result<GetAssetTypesQueryResult>>
{
  public async Task<Result<GetAssetTypesQueryResult>> Handle(GetAssetTypesQuery query, CancellationToken cancellationToken)
  {
    var types = context.AssetTypes.AsNoTracking();

    if (query.AssetClassId.HasValue)
    {
      var classId = AssetClassId.Of(query.AssetClassId.Value);
      types = types.Where(t => t.AssetClassId == classId);
    }

    if (!query.IncludeInactive)
      types = types.Where(t => t.IsActive);

    var data = await types
      .OrderBy(t => t.DisplayOrder)
      .ThenBy(t => t.Code)
      .ToListAsync(cancellationToken);

    return Result<GetAssetTypesQueryResult>.Success(new GetAssetTypesQueryResult(data.Select(t => t.ToDto()).ToList()));
  }
}
