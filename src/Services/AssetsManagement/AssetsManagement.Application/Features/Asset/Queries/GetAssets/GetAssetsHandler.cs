using Microsoft.EntityFrameworkCore;

public class GetAssetsHandler(IApplicationDbContext context, IAttributeSchemaService schemaService)
  : IQueryHandler<GetAssetsQuery, Result<GetAssetsQueryResult>>
{
  public async Task<Result<GetAssetsQueryResult>> Handle(GetAssetsQuery query, CancellationToken cancellationToken)
  {
    var assets = await AssetQueryFilter.ApplyAsync(context, query, cancellationToken);
    var acquisitions = context.AssetAcquisitions.AsNoTracking();
    var disposals = context.AssetDisposals.AsNoTracking();

    var totalCount = await assets.LongCountAsync(cancellationToken);

    var page = await AssetQueryFilter.Order(context, assets, query)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    // purchase and disposal facts of the page, for the register's columns
    var pageIds = page.Select(a => a.Id).ToList();
    var pageAcquisitions = await acquisitions.Where(q => pageIds.Contains(q.AssetId)).ToDictionaryAsync(q => q.AssetId, cancellationToken);
    var pageDisposals = await disposals.Where(d => pageIds.Contains(d.AssetId)).ToDictionaryAsync(d => d.AssetId, cancellationToken);

    // grid columns: attributes flagged is_visible_in_list for each (class, type, category) combination on the page
    var listCodes = new Dictionary<(AssetClassId, AssetTypeId, AssetCategoryId), HashSet<string>>();
    var data = new List<AssetListItemDto>(page.Count);
    foreach (var asset in page)
    {
      var key = (asset.AssetClassId, asset.AssetTypeId, asset.CategoryId);
      if (!listCodes.TryGetValue(key, out var codes))
      {
        var schema = await schemaService.ResolveAsync(asset.AssetClassId, asset.AssetTypeId, asset.CategoryId, null, cancellationToken);
        codes = schema.Attributes.Where(a => a.Assignment.IsVisibleInList).Select(a => a.Code).ToHashSet(StringComparer.Ordinal);
        listCodes[key] = codes;
      }

      data.Add(asset.ToListItemDto(codes, pageAcquisitions.GetValueOrDefault(asset.Id), pageDisposals.GetValueOrDefault(asset.Id)));
    }

    return Result<GetAssetsQueryResult>.Success(new GetAssetsQueryResult(
      new PaginatedResult<AssetListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, totalCount, data)));
  }
}
