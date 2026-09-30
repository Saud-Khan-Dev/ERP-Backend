using Microsoft.EntityFrameworkCore;

public class GetPropertyEncumbrancesHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyEncumbrancesQuery, Result<GetPropertyEncumbrancesQueryResult>>
{
  public async Task<Result<GetPropertyEncumbrancesQueryResult>> Handle(GetPropertyEncumbrancesQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    var encumbrances = context.Encumbrances.AsNoTracking().Where(e => e.PropertyId == propertyId);

    if (query.Status is { } status)
      encumbrances = encumbrances.Where(e => e.Status == status);

    var rows = await encumbrances.OrderBy(e => e.Status).ThenByDescending(e => e.StartDate).ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<EncumbranceType>(rows.Select(e => e.EncumbranceTypeId)).LoadAsync(cancellationToken);
    var owners = await read.OwnerRefsAsync(rows.Select(e => e.HolderOwnerId), cancellationToken);

    return Result<GetPropertyEncumbrancesQueryResult>.Success(
      new GetPropertyEncumbrancesQueryResult(rows.Select(e => e.ToDto(refs, owners)).ToList()));
  }
}
