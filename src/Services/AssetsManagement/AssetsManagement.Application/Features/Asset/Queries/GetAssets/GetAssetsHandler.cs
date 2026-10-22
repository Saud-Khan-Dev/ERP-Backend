using Microsoft.EntityFrameworkCore;

public class GetAssetsHandler(IApplicationDbContext context, IAttributeSchemaService schemaService, IAssetDataQueries queries, IHrmDirectory hrm, IClock clock)
  : IQueryHandler<GetAssetsQuery, Result<GetAssetsQueryResult>>
{
  public async Task<Result<GetAssetsQueryResult>> Handle(GetAssetsQuery query, CancellationToken cancellationToken)
  {
    var extra = await AssetFilterContext.BuildAsync(query, queries, hrm, clock, cancellationToken);
    var assets = await AssetQueryFilter.ApplyAsync(context, query, extra, cancellationToken);

    var totalCount = await assets.LongCountAsync(cancellationToken);

    var page = await AssetQueryFilter.Order(context, assets, query)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    // purchase, disposal, loan and status facts of the page, for the register's columns
    var pageIds = page.Select(a => a.Id).ToList();
    var pageAcquisitions = await context.AssetAcquisitions.AsNoTracking().Where(q => pageIds.Contains(q.AssetId)).ToDictionaryAsync(q => q.AssetId, cancellationToken);
    var pageDisposals = await context.AssetDisposals.AsNoTracking().Where(d => pageIds.Contains(d.AssetId) && d.State == DisposalState.Approved)
      .ToDictionaryAsync(d => d.AssetId, cancellationToken);
    var openLoans = (await context.AssetAssignments.AsNoTracking()
        .Where(x => pageIds.Contains(x.AssetId) && x.ExpectedReturnDate != null && x.ActualReturnDate == null)
        .ToListAsync(cancellationToken))
      .GroupBy(x => x.AssetId).ToDictionary(g => g.Key, g => g.Min(x => x.ExpectedReturnDate));
    var statusEvents = (await context.AssetLifecycleEvents.AsNoTracking()
        .Where(e => pageIds.Contains(e.AssetId) && e.ToStatusId != null)
        .Select(e => new { e.AssetId, e.ToStatusId, e.EventDate })
        .ToListAsync(cancellationToken));

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

      var since = statusEvents.Where(e => e.AssetId == asset.Id && e.ToStatusId == asset.StatusId).Select(e => (DateTime?)e.EventDate).Max() ?? asset.CreatedAt;
      data.Add(asset.ToListItemDto(codes, pageAcquisitions.GetValueOrDefault(asset.Id), pageDisposals.GetValueOrDefault(asset.Id),
        query.IncludeMoney, openLoans.GetValueOrDefault(asset.Id), since));
    }

    return Result<GetAssetsQueryResult>.Success(new GetAssetsQueryResult(
      new PaginatedResult<AssetListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, totalCount, data)));
  }
}
