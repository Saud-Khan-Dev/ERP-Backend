using Microsoft.EntityFrameworkCore;

public class GetLifecycleTimelineHandler(IApplicationDbContext context)
  : IQueryHandler<GetLifecycleTimelineQuery, Result<GetLifecycleTimelineQueryResult>>
{
  public async Task<Result<GetLifecycleTimelineQueryResult>> Handle(GetLifecycleTimelineQuery query, CancellationToken cancellationToken)
  {
    var events = context.AssetLifecycleEvents.AsNoTracking();

    if (query.AssetId.HasValue)
    {
      var assetId = AssetId.Of(query.AssetId.Value);
      events = events.Where(e => e.AssetId == assetId);
    }

    if (query.EventTypeId.HasValue)
    {
      var eventTypeId = LifecycleEventTypeId.Of(query.EventTypeId.Value);
      events = events.Where(e => e.EventTypeId == eventTypeId);
    }

    if (!string.IsNullOrWhiteSpace(query.Stage))
    {
      var stage = query.Stage.Trim().ToUpperInvariant();
      var typeIds = (await context.LifecycleEventTypes.AsNoTracking().ToListAsync(cancellationToken))
        .Where(t => string.Equals(t.Stage, stage, StringComparison.OrdinalIgnoreCase))
        .Select(t => t.Id)
        .ToList();
      events = events.Where(e => typeIds.Contains(e.EventTypeId));
    }

    if (query.From.HasValue)
    {
      var from = DateTime.SpecifyKind(query.From.Value, DateTimeKind.Utc);
      events = events.Where(e => e.EventDate >= from);
    }

    if (query.To.HasValue)
    {
      var to = DateTime.SpecifyKind(query.To.Value, DateTimeKind.Utc);
      events = events.Where(e => e.EventDate <= to);
    }

    var total = await events.LongCountAsync(cancellationToken);
    var page = await events
      .OrderByDescending(e => e.EventDate)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
      .Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var assetIds = page.Select(e => e.AssetId).Distinct().ToList();
    var assets = await context.Assets.IgnoreQueryFilters().AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
    var types = await context.LifecycleEventTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);

    var data = page.Select(e =>
    {
      var asset = assets.GetValueOrDefault(e.AssetId);
      var type = types.GetValueOrDefault(e.EventTypeId);
      return new LifecycleTimelineItemDto(
        e.Id.Value, e.AssetId.Value, asset?.AssetCode.Value ?? "", asset?.Name.Value ?? "",
        e.EventTypeId.Value, type?.Code.Value ?? "", type?.Name.Value ?? "", type?.Stage,
        e.EventDate, e.FromStatusId?.Value, e.ToStatusId?.Value, e.PerformedBy, e.Notes, e.Details);
    }).ToList();

    return Result<GetLifecycleTimelineQueryResult>.Success(new GetLifecycleTimelineQueryResult(
      new PaginatedResult<LifecycleTimelineItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
