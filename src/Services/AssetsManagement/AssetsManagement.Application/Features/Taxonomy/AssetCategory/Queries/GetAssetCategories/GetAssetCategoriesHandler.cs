using Microsoft.EntityFrameworkCore;

public class GetAssetCategoriesHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetCategoriesQuery, Result<GetAssetCategoriesQueryResult>>
{
  public async Task<Result<GetAssetCategoriesQueryResult>> Handle(GetAssetCategoriesQuery query, CancellationToken cancellationToken)
  {
    var categories = context.AssetCategories.AsNoTracking();

    if (query.AssetClassId.HasValue)
    {
      var classId = AssetClassId.Of(query.AssetClassId.Value);
      categories = categories.Where(c => c.AssetClassId == classId);
    }

    if (query.AssetTypeId.HasValue)
    {
      var typeId = AssetTypeId.Of(query.AssetTypeId.Value);
      categories = categories.Where(c => c.AssetTypeId == typeId);
    }

    if (query.ParentCategoryId.HasValue)
    {
      var parentId = AssetCategoryId.Of(query.ParentCategoryId.Value);
      categories = categories.Where(c => c.ParentCategoryId == parentId);
    }
    else if (query.OnlyRoots)
    {
      categories = categories.Where(c => c.ParentCategoryId == null);
    }

    if (query.OnlyLeaves)
      categories = categories.Where(c => c.IsLeaf);

    if (!query.IncludeInactive)
      categories = categories.Where(c => c.IsActive);

    var data = await categories
      .OrderBy(c => c.Depth)
      .ThenBy(c => c.DisplayOrder)
      .ThenBy(c => c.Code)
      .ToListAsync(cancellationToken);

    return Result<GetAssetCategoriesQueryResult>.Success(new GetAssetCategoriesQueryResult(data.Select(c => c.ToDto()).ToList()));
  }
}
