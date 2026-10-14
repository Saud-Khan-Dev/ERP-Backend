using Microsoft.EntityFrameworkCore;

public class GetPropertiesHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertiesQuery, Result<GetPropertiesQueryResult>>
{
  public async Task<Result<GetPropertiesQueryResult>> Handle(GetPropertiesQuery query, CancellationToken cancellationToken)
  {
    var properties = await PropertyQueryFilter.ApplyAsync(context, query, cancellationToken);

    var total = await properties.LongCountAsync(cancellationToken);

    var page = await PropertyQueryFilter.Order(context, properties, query)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs()
        .Add<Town>(page.Select(p => p.TownId))
        .Add<PropertyType>(page.Select(p => p.PropertyTypeId))
        .Add<PropertyStatus>(page.Select(p => p.PropertyStatusId))
        .Add<PropertyClassification>(page.Select(p => p.PropertyClassificationId))
        .LoadAsync(cancellationToken);

    // the page's area and owner columns
    var pageIds = page.Select(p => p.Id).ToList();
    var areas = await read.CurrentTotalAreasAsync(pageIds, cancellationToken);
    var holders = await HolderSummariesAsync(pageIds, cancellationToken);
    var allottees = await AllotteesAsync(pageIds.Where(id => !holders.ContainsKey(id)).ToList(), cancellationToken);

    var data = page.Select(p => p.ToListItemDto(
      refs,
      areas.TryGetValue(p.Id, out var area) ? area : null,
      holders.GetValueOrDefault(p.Id),
      allottees.GetValueOrDefault(p.Id))).ToList();

    return Result<GetPropertiesQueryResult>.Success(new GetPropertiesQueryResult(
      new PaginatedResult<PropertyListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  /// Per property: the first holder (largest current share, then name) and how many owners hold a current share.
  private async Task<Dictionary<PropertyId, OwnerSummaryDto>> HolderSummariesAsync(List<PropertyId> propertyIds, CancellationToken cancellationToken)
  {
    if (propertyIds.Count == 0)
      return [];

    var holdings = await PropertyQueryFilter.CurrentHoldings(context)
        .Where(o => propertyIds.Contains(o.PropertyId))
        .Select(o => new { o.PropertyId, o.OwnerId, o.OwnershipSharePct })
        .ToListAsync(cancellationToken);

    var ownerIds = holdings.Select(h => h.OwnerId).Distinct().ToList();
    var names = (await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).Select(o => new { o.Id, o.OwnerName }).ToListAsync(cancellationToken))
      .ToDictionary(o => o.Id, o => o.OwnerName.Value);

    return holdings
      .GroupBy(h => h.PropertyId)
      .ToDictionary(g => g.Key, g =>
      {
        var byOwner = g.GroupBy(h => h.OwnerId)
          .Select(o => (Name: names.GetValueOrDefault(o.Key, string.Empty), Share: o.Sum(x => x.OwnershipSharePct)))
          .OrderByDescending(o => o.Share).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
          .ToList();
        return new OwnerSummaryDto(byOwner[0].Name, byOwner.Count);
      });
  }

  /// For properties nobody holds: the allottee of the allotment in force (the latest one, should there be more).
  private async Task<Dictionary<PropertyId, string>> AllotteesAsync(List<PropertyId> propertyIds, CancellationToken cancellationToken)
  {
    if (propertyIds.Count == 0)
      return [];

    var inForce = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
    var allotments = await context.Allotments.AsNoTracking()
        .Where(a => propertyIds.Contains(a.PropertyId) && a.IsActive && inForce.Contains(a.AllotmentStatusId))
        .Select(a => new { a.PropertyId, a.AllotteeOwnerId, a.AllotmentDate })
        .ToListAsync(cancellationToken);

    var ownerIds = allotments.Select(a => a.AllotteeOwnerId).Distinct().ToList();
    var names = (await context.Owners.AsNoTracking().Where(o => ownerIds.Contains(o.Id)).Select(o => new { o.Id, o.OwnerName }).ToListAsync(cancellationToken))
      .ToDictionary(o => o.Id, o => o.OwnerName.Value);

    return allotments
      .GroupBy(a => a.PropertyId)
      .ToDictionary(g => g.Key, g => names.GetValueOrDefault(g.OrderByDescending(a => a.AllotmentDate).First().AllotteeOwnerId, string.Empty));
  }
}
