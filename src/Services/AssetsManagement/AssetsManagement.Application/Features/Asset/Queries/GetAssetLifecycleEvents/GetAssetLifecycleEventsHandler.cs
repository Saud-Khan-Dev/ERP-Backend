using Microsoft.EntityFrameworkCore;

public class GetAssetLifecycleEventsHandler(IApplicationDbContext context)
  : IQueryHandler<GetAssetLifecycleEventsQuery, Result<GetAssetLifecycleEventsQueryResult>>
{
  public async Task<Result<GetAssetLifecycleEventsQueryResult>> Handle(GetAssetLifecycleEventsQuery query, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(query.AssetId);
    if (!await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.Id == assetId, cancellationToken))
      throw new AssetNotFoundException($"Asset {query.AssetId} was not found.");

    var events = context.AssetLifecycleEvents.AsNoTracking().Where(e => e.AssetId == assetId);

    if (query.EventTypeId.HasValue)
    {
      var eventTypeId = LifecycleEventTypeId.Of(query.EventTypeId.Value);
      events = events.Where(e => e.EventTypeId == eventTypeId);
    }

    var data = await events.OrderByDescending(e => e.EventDate).ToListAsync(cancellationToken);

    return Result<GetAssetLifecycleEventsQueryResult>.Success(new GetAssetLifecycleEventsQueryResult(data.Select(e => e.ToDto()).ToList()));
  }
}
