using Microsoft.EntityFrameworkCore;

public class GetUserSessionsHandler(IApplicationDbContext context)
  : IQueryHandler<GetUserSessionsQuery, Result<GetUserSessionsQueryResult>>
{
  public async Task<Result<GetUserSessionsQueryResult>> Handle(GetUserSessionsQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(query.UserId);

    if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException($"User {query.UserId} was not found.");

    var sessions = context.Sessions.AsNoTracking().Where(s => s.UserId == userId);

    if (!query.IncludeRevoked)
      sessions = sessions.Where(s => s.RevokedAt == null && s.ExpiresAt > now);

    var data = await sessions.OrderByDescending(s => s.IssuedAt).ToListAsync(cancellationToken);

    return Result<GetUserSessionsQueryResult>.Success(
      new GetUserSessionsQueryResult(data.Select(s => s.ToDto(now)).ToList()));
  }
}
