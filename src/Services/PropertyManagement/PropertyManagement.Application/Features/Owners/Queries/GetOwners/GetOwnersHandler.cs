using Microsoft.EntityFrameworkCore;

public class GetOwnersHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetOwnersQuery, Result<GetOwnersQueryResult>>
{
  public async Task<Result<GetOwnersQueryResult>> Handle(GetOwnersQuery query, CancellationToken cancellationToken)
  {
    var owners = context.Owners.AsNoTracking();

    // a current holding: an active or disputed share on a property still on the register
    var activeProperties = context.Properties.AsNoTracking().Where(p => p.IsActive).Select(p => p.Id);
    var holdings = context.Ownerships.AsNoTracking()
        .Where(o => (o.OwnershipStatus == OwnershipStatus.Active || o.OwnershipStatus == OwnershipStatus.Disputed)
          && activeProperties.Contains(o.PropertyId));

    if (!query.IncludeInactive)
      owners = owners.Where(o => o.IsActive);

    if (query.OwnerTypeId is { } typeId)
      owners = owners.Where(o => o.OwnerTypeId == MasterId.Of(typeId));

    if (Cnic.Normalize(query.Cnic) is { } normalizedCnic)
    {
      var cnic = Cnic.FromStored(normalizedCnic);
      owners = owners.Where(o => o.Cnic == cnic);
    }

    if (Ntn.Normalize(query.Ntn) is { } ntn)
      owners = owners.Where(OwnerSearch.NtnIs(ntn));

    if (query.HasHoldings is { } hasHoldings)
      owners = hasHoldings
        ? owners.Where(o => holdings.Any(h => h.OwnerId == o.Id))
        : owners.Where(o => !holdings.Any(h => h.OwnerId == o.Id));

    if (!string.IsNullOrWhiteSpace(query.Search))
      owners = OwnerSearch.Matching(owners, query.Search);

    var total = await owners.LongCountAsync(cancellationToken);

    var types = context.Set<OwnerType>().AsNoTracking();
    var desc = query.SortDescending;
    IOrderedQueryable<PropertyOwner> ordered = query.SortBy switch
    {
      OwnerSort.Name => desc ? owners.OrderByDescending(o => o.OwnerName.Value) : owners.OrderBy(o => o.OwnerName.Value),
      OwnerSort.Type => desc
        ? owners.OrderByDescending(o => types.Where(t => t.Id == o.OwnerTypeId).Select(t => t.Name).FirstOrDefault())
        : owners.OrderBy(o => types.Where(t => t.Id == o.OwnerTypeId).Select(t => t.Name).FirstOrDefault()),
      OwnerSort.Holdings => desc
        ? owners.OrderByDescending(o => holdings.Count(h => h.OwnerId == o.Id))
        : owners.OrderBy(o => holdings.Count(h => h.OwnerId == o.Id)),
      _ => desc ? owners.OrderByDescending(o => o.OwnerCode) : owners.OrderBy(o => o.OwnerCode),
    };

    var page = await ordered.ThenBy(o => o.OwnerCode)
        .Include(o => o.Contacts.Where(c => c.IsPrimary))
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    var pageIds = page.Select(o => o.Id).ToList();
    var counts = await holdings.Where(h => pageIds.Contains(h.OwnerId))
        .GroupBy(h => h.OwnerId)
        .Select(g => new { OwnerId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.OwnerId, x => x.Count, cancellationToken);

    var refs = await masters.Refs().Add<OwnerType>(page.Select(o => o.OwnerTypeId)).LoadAsync(cancellationToken);

    return Result<GetOwnersQueryResult>.Success(new GetOwnersQueryResult(
      new PaginatedResult<OwnerListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
        page.Select(o => o.ToListItemDto(refs, counts.GetValueOrDefault(o.Id))).ToList())));
  }
}
