using Microsoft.EntityFrameworkCore;

public class GetPropertyOwnershipsHandler(IApplicationDbContext context, PropertyReadService read)
  : IQueryHandler<GetPropertyOwnershipsQuery, Result<GetOwnershipsQueryResult>>,
    IQueryHandler<GetOwnerOwnershipsQuery, Result<GetOwnershipsQueryResult>>
{
  public async Task<Result<GetOwnershipsQueryResult>> Handle(GetPropertyOwnershipsQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    return await BuildAsync(context.Ownerships.Where(o => o.PropertyId == propertyId), query.IncludeHistory, withTotal: true, cancellationToken);
  }

  public async Task<Result<GetOwnershipsQueryResult>> Handle(GetOwnerOwnershipsQuery query, CancellationToken cancellationToken)
  {
    var ownerId = OwnerId.Of(query.OwnerId);

    if (!await context.Owners.AnyAsync(o => o.Id == ownerId, cancellationToken))
      throw new OwnerNotFoundException($"Owner {query.OwnerId} was not found.");

    return await BuildAsync(context.Ownerships.Where(o => o.OwnerId == ownerId), query.IncludeHistory, withTotal: false, cancellationToken);
  }

  private async Task<Result<GetOwnershipsQueryResult>> BuildAsync(IQueryable<PropertyOwnership> ownerships, bool includeHistory, bool withTotal, CancellationToken cancellationToken)
  {
    if (!includeHistory)
      ownerships = ownerships.Where(o => o.OwnershipStatus == OwnershipStatus.Active);

    var rows = await ownerships.AsNoTracking()
        .OrderBy(o => o.OwnershipStatus).ThenByDescending(o => o.EffectiveFrom)
        .ToListAsync(cancellationToken);

    return Result<GetOwnershipsQueryResult>.Success(new GetOwnershipsQueryResult(
      withTotal ? OwnershipShares.Total(rows) : null,
      await read.OwnershipDtosAsync(rows, cancellationToken)));
  }
}
