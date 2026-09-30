using Microsoft.EntityFrameworkCore;

public class GetStatusHistoryHandler(IApplicationDbContext context, MasterLookup masters)
  : IQueryHandler<GetStatusHistoryQuery, Result<GetStatusHistoryQueryResult>>
{
  public async Task<Result<GetStatusHistoryQueryResult>> Handle(GetStatusHistoryQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);
    await EnsurePropertyExistsAsync(propertyId, cancellationToken);

    var history = await context.PropertyStatusHistories.AsNoTracking()
        .Where(h => h.PropertyId == propertyId)
        .OrderByDescending(h => h.EffectiveFrom).ThenByDescending(h => h.CreatedAt)
        .ToListAsync(cancellationToken);

    var refs = await masters.Refs().Add<PropertyStatus>(history.Select(h => h.PropertyStatusId)).LoadAsync(cancellationToken);

    return Result<GetStatusHistoryQueryResult>.Success(
      new GetStatusHistoryQueryResult(history.Select(h => h.ToDto(refs)).ToList()));
  }

  private async Task EnsurePropertyExistsAsync(PropertyId propertyId, CancellationToken cancellationToken)
  {
    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {propertyId.Value} was not found.");
  }
}
