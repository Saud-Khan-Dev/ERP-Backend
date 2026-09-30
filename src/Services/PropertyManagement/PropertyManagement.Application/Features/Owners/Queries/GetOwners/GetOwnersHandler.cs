using Microsoft.EntityFrameworkCore;

public class GetOwnersHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetOwnersQuery, Result<GetOwnersQueryResult>>
{
  public async Task<Result<GetOwnersQueryResult>> Handle(GetOwnersQuery query, CancellationToken cancellationToken)
  {
    var owners = context.Owners.AsNoTracking();

    if (!query.IncludeInactive)
      owners = owners.Where(o => o.IsActive);

    if (query.OwnerTypeId is { } typeId)
      owners = owners.Where(o => o.OwnerTypeId == MasterId.Of(typeId));

    if (!string.IsNullOrWhiteSpace(query.Cnic))
    {
      var cnic = Cnic.Of(query.Cnic);
      owners = owners.Where(o => o.Cnic == cnic);
    }

    if (!string.IsNullOrWhiteSpace(query.Ntn))
    {
      var ntn = query.Ntn.Trim().ToUpperInvariant();
      owners = owners.Where(o => o.Ntn == ntn);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();

      BusinessCode? code = null;
      Cnic? cnic = null;
      try { code = BusinessCode.Of(term); } catch (DomainException) { }
      try { cnic = Cnic.Of(term); } catch (DomainException) { }

      var pattern = $"%{term.ToLowerInvariant()}%";
      owners = owners.Where(o =>
          (code != null && o.OwnerCode == code)
          || (cnic != null && o.Cnic == cnic)
          || EF.Functions.Like(o.OwnerName.Value.ToLower(), pattern));
    }

    var total = await owners.LongCountAsync(cancellationToken);

    var page = await owners
        .Include(o => o.Contacts.Where(c => c.IsPrimary))
        .OrderBy(o => o.OwnerCode)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<OwnerType>(page.Select(o => o.OwnerTypeId)).LoadAsync(cancellationToken);

    return Result<GetOwnersQueryResult>.Success(new GetOwnersQueryResult(
      new PaginatedResult<OwnerListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total,
        page.Select(o => o.ToListItemDto(refs)).ToList())));
  }
}
