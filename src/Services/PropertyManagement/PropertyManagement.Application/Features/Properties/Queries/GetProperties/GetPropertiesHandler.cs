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
    var ownerCounts = await context.Ownerships.AsNoTracking()
        .Where(o => pageIds.Contains(o.PropertyId) && o.OwnershipStatus == OwnershipStatus.Active)
        .GroupBy(o => o.PropertyId)
        .Select(g => new { PropertyId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.PropertyId, x => x.Count, cancellationToken);

    var data = page.Select(p => p.ToListItemDto(
      refs,
      areas.TryGetValue(p.Id, out var area) ? area : null,
      ownerCounts.GetValueOrDefault(p.Id))).ToList();

    return Result<GetPropertiesQueryResult>.Success(new GetPropertiesQueryResult(
      new PaginatedResult<PropertyListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
