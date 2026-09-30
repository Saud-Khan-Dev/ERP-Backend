using Microsoft.EntityFrameworkCore;

/// The property with its masters resolved, its area summary and its current owners.
public class GetPropertyHandler(IApplicationDbContext context, MasterLookup masters, PropertyReadService read)
  : IQueryHandler<GetPropertyQuery, Result<GetPropertyQueryResult>>
{
  public async Task<Result<GetPropertyQueryResult>> Handle(GetPropertyQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.Id);
    var property = await context.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId, cancellationToken)
      ?? throw new PropertyNotFoundException($"Property {query.Id} was not found.");

    var refs = await masters.Refs()
        .Add<Town>(property.TownId)
        .Add<PropertyType>(property.PropertyTypeId)
        .Add<PropertyStatus>(property.PropertyStatusId)
        .Add<PropertyClassification>(property.PropertyClassificationId)
        .LoadAsync(cancellationToken);

    var currentOwners = await context.Ownerships.AsNoTracking()
        .Where(o => o.PropertyId == propertyId && o.OwnershipStatus == OwnershipStatus.Active)
        .OrderByDescending(o => o.OwnershipSharePct)
        .ToListAsync(cancellationToken);

    var dto = property.ToDto(
      refs,
      await read.AreaSummaryAsync(propertyId, cancellationToken),
      await read.OwnershipDtosAsync(currentOwners, cancellationToken));

    return Result<GetPropertyQueryResult>.Success(new GetPropertyQueryResult(dto));
  }
}
