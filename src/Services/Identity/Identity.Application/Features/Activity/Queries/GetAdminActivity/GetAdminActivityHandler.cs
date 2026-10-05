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

    if (!string.IsNullOrWhiteSpace(query.Action) && Enum.TryParse<ActivityAction>(query.Action, ignoreCase: true, out var action))
      activities = activities.Where(a => a.Action == action);

    if (query.From is { } from)
      activities = activities.Where(a => a.OccurredAt >= from);

    if (query.To is { } to)
      activities = activities.Where(a => a.OccurredAt <= to);

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
}
