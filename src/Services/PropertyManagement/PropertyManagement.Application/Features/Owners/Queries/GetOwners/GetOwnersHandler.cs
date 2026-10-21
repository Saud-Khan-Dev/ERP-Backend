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

    if (query.Role is { } role)
      owners = await OwnerRoles.WithRoleAsync(context, owners, role, cancellationToken);

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

public static class OwnerRoles
{
  /// The owners playing that part (agreements: in force; on active properties for shares).
  public static async Task<IQueryable<PropertyOwner>> WithRoleAsync(IApplicationDbContext context, IQueryable<PropertyOwner> owners, OwnerRole role, CancellationToken cancellationToken)
  {
    var active = context.Properties.AsNoTracking().Where(p => p.IsActive).Select(p => p.Id);
    switch (role)
    {
      case OwnerRole.Holder:
        var holdings = PropertyQueryFilter.CurrentHoldings(context).Where(h => active.Contains(h.PropertyId));
        return owners.Where(o => holdings.Any(h => h.OwnerId == o.Id));
      case OwnerRole.Allottee:
        var allotment = await InForce.StatusIdsAsync<AllotmentStatus>(context, InForce.AllotmentStatuses, cancellationToken);
        var allotments = context.Allotments.AsNoTracking().Where(a => a.IsActive && allotment.Contains(a.AllotmentStatusId));
        return owners.Where(o => allotments.Any(a => a.AllotteeOwnerId == o.Id));
      case OwnerRole.Lessee:
        var lease = await InForce.StatusIdsAsync<LeaseStatus>(context, InForce.LeaseStatuses, cancellationToken);
        var leases = context.Leases.AsNoTracking().Where(l => lease.Contains(l.LeaseStatusId));
        return owners.Where(o => leases.Any(l => l.LesseeOwnerId == o.Id));
      case OwnerRole.Tenant:
        var rental = await InForce.StatusIdsAsync<RentalStatus>(context, InForce.RentalStatuses, cancellationToken);
        var rentals = context.Rentals.AsNoTracking().Where(r => rental.Contains(r.RentalStatusId));
        return owners.Where(o => rentals.Any(r => r.TenantOwnerId == o.Id));
      case OwnerRole.Contractor:
        var contract = await InForce.StatusIdsAsync<ContractStatus>(context, InForce.ContractStatuses, cancellationToken);
        var contracts = context.Outsourcings.AsNoTracking().Where(c => contract.Contains(c.ContractStatusId));
        return owners.Where(o => contracts.Any(c => c.OutsourcedPartyOwnerId == o.Id));
      case OwnerRole.Bidder:
        var bids = context.Auctions.AsNoTracking().SelectMany(a => a.Bids);
        var winners = context.Auctions.AsNoTracking().Where(a => a.SuccessfulBidderOwnerId != null);
        return owners.Where(o => bids.Any(b => b.BidderOwnerId == o.Id) || winners.Any(a => a.SuccessfulBidderOwnerId == o.Id));
      case OwnerRole.Encroacher:
        var encroachments = context.Encroachments.AsNoTracking().Where(e => e.EncroacherOwnerId != null);
        return owners.Where(o => encroachments.Any(e => e.EncroacherOwnerId == o.Id));
      case OwnerRole.Litigant:
        var parties = context.Litigations.AsNoTracking().SelectMany(l => l.Parties).Where(p => p.PartyOwnerId != null);
        return owners.Where(o => parties.Any(p => p.PartyOwnerId == o.Id));
      case OwnerRole.Appellant:
        var appeals = context.Appeals.AsNoTracking();
        return owners.Where(o => appeals.Any(a => a.AppellantOwnerId == o.Id));
      default:
        var violations = context.Violations.AsNoTracking();
        return owners.Where(o => violations.Any(v => v.ViolatorOwnerId == o.Id));
    }
  }
}
