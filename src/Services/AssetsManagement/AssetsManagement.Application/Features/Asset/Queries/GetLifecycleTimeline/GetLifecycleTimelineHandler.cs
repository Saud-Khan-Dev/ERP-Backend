using Microsoft.EntityFrameworkCore;

public class GetLifecycleTimelineHandler(IApplicationDbContext context)
  : IQueryHandler<GetLifecycleTimelineQuery, Result<GetLifecycleTimelineQueryResult>>
{
  public async Task<Result<GetLifecycleTimelineQueryResult>> Handle(GetLifecycleTimelineQuery query, CancellationToken cancellationToken)
  {
    // the assets' own query filter leaves deleted assets - and so their events - out
    var assets = context.Assets.AsNoTracking();

    if (query.AssetClassId.HasValue)
    {
      var classId = AssetClassId.Of(query.AssetClassId.Value);
      assets = assets.Where(a => a.AssetClassId == classId);
    }

    if (query.CategoryId.HasValue)
    {
      var categoryId = AssetCategoryId.Of(query.CategoryId.Value);
      var root = await context.AssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
        ?? throw new DomainException("The category in the filter was not found. Clear the filter.");
      var subtree = (await context.AssetCategories.AsNoTracking().Where(c => c.AssetTypeId == root.AssetTypeId).ToListAsync(cancellationToken))
        .Where(c => c.Id == root.Id || root.IsAncestorOf(c)).Select(c => c.Id).ToList();
      assets = assets.Where(a => subtree.Contains(a.CategoryId));
    }

    if (query.LocationId.HasValue)
    {
      var locationId = LocationId.Of(query.LocationId.Value);
      var root = await context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken)
        ?? throw new DomainException("The location in the filter was not found. Clear the filter.");
      var prefix = root.Path + Location.PathSeparator;
      var subtree = (await context.Locations.AsNoTracking().ToListAsync(cancellationToken))
        .Where(l => l.Id == root.Id || (root.Path != null && l.Path != null && l.Path.StartsWith(prefix, StringComparison.Ordinal)))
        .Select(l => (LocationId?)l.Id).ToList();
      assets = assets.Where(a => subtree.Contains(a.CurrentLocationId));
    }

    var events = context.AssetLifecycleEvents.AsNoTracking().Where(e => assets.Any(a => a.Id == e.AssetId));

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
    var names = await context.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
    var types = await context.LifecycleEventTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, cancellationToken);

    var data = page.Select(e =>
    {
      var asset = names.GetValueOrDefault(e.AssetId);
      var type = types.GetValueOrDefault(e.EventTypeId);
      return new LifecycleTimelineItemDto(
        e.Id.Value, e.AssetId.Value, asset?.AssetCode.Value ?? "", asset?.Name.Value ?? "",
        e.EventTypeId.Value, type?.Code.Value ?? "", type?.Name.Value ?? "", type?.Stage, type?.Purpose,
        e.EventDate, e.FromStatusId?.Value, e.ToStatusId?.Value, e.PerformedBy, e.PerformedByName, e.Notes, e.Details);
    }).ToList();

    return Result<GetLifecycleTimelineQueryResult>.Success(new GetLifecycleTimelineQueryResult(
      new PaginatedResult<LifecycleTimelineItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }
}
