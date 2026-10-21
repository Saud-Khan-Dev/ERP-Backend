using Microsoft.EntityFrameworkCore;

public class GetAdminActivityHandler(IApplicationDbContext context)
  : IQueryHandler<GetAdminActivityQuery, Result<GetAdminActivityQueryResult>>
{
  public async Task<Result<GetAdminActivityQueryResult>> Handle(GetAdminActivityQuery query, CancellationToken cancellationToken)
  {
    var activities = context.AdminActivities.AsNoTracking();

    if (query.ActorUserId is { } actorId)
    {
      var id = UserId.Of(actorId);
      activities = activities.Where(a => a.ActorUserId == id);
    }

    if (query.TargetId is { } targetId)
      activities = activities.Where(a => a.TargetId == targetId);

    // everything a person did, and everything done to their account
    if (query.PersonId is { } personId)
    {
      var asActor = UserId.Of(personId);
      activities = activities.Where(a => a.ActorUserId == asActor || a.TargetId == personId);
    }

    if (!string.IsNullOrWhiteSpace(query.Kind) && ActivityKinds.Actions.TryGetValue(query.Kind, out var kindActions))
      activities = activities.Where(a => kindActions.Contains(a.Action));

    if (!string.IsNullOrWhiteSpace(query.Action) && Enum.TryParse<ActivityAction>(query.Action, ignoreCase: true, out var action))
      activities = activities.Where(a => a.Action == action);

    // a date without a zone is read as UTC (the screens send UTC)
    if (query.From is { } from)
    {
      var since = AsUtc(from);
      activities = activities.Where(a => a.OccurredAt >= since);
    }

    if (query.To is { } to)
    {
      var until = AsUtc(to);
      activities = activities.Where(a => a.OccurredAt <= until);
    }

    var totalCount = await activities.LongCountAsync(cancellationToken);

    var page = await activities
        .OrderByDescending(a => a.OccurredAt)
        .ThenByDescending(a => a.Id)
        .Skip(query.Pagination.Pageindex * query.Pagination.PageSize)
        .Take(query.Pagination.PageSize)
        .ToListAsync(cancellationToken);

    return Result<GetAdminActivityQueryResult>.Success(new GetAdminActivityQueryResult(
      new PaginatedResult<AdminActivityDto>(
        query.Pagination.Pageindex, query.Pagination.PageSize, totalCount,
        page.Select(a => a.ToDto()).ToList())));
  }

  private static DateTime AsUtc(DateTime value) =>
    value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
