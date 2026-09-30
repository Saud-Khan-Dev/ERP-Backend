using Microsoft.EntityFrameworkCore;

public class GetAreaSummaryHandler(IApplicationDbContext context, PropertyReadService read)
  : IQueryHandler<GetAreaSummaryQuery, Result<GetAreaSummaryQueryResult>>
{
  public async Task<Result<GetAreaSummaryQueryResult>> Handle(GetAreaSummaryQuery query, CancellationToken cancellationToken)
  {
    var propertyId = PropertyId.Of(query.PropertyId);

    if (!await context.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken))
      throw new PropertyNotFoundException($"Property {query.PropertyId} was not found.");

    return Result<GetAreaSummaryQueryResult>.Success(
      new GetAreaSummaryQueryResult(await read.AreaSummaryAsync(propertyId, cancellationToken)));
  }
}
