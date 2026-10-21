using Microsoft.EntityFrameworkCore;

public class GetUserSessionsHandler(IApplicationDbContext context)
  : IQueryHandler<GetUserSessionsQuery, Result<GetUserSessionsQueryResult>>
{
  public async Task<Result<GetUserSessionsQueryResult>> Handle(GetUserSessionsQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(query.UserId);

    if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException("This account was not found - it may have been deleted.");

    var sessions = context.Sessions.AsNoTracking().Where(s => s.UserId == userId);

    // ended sessions are the real endings (signed out, password changed, ran out) - a rotation is the same sign-in going
    // on, never a device of its own - and only the latest 50
    sessions = query.IncludeRevoked
      ? sessions.Where(s => s.RevokedReason == null || s.RevokedReason != "Rotated")
      : sessions.Where(s => s.RevokedAt == null && s.ExpiresAt > now);

    var data = await sessions.OrderByDescending(s => s.IssuedAt).Take(50).ToListAsync(cancellationToken);

    return Result<GetUserSessionsQueryResult>.Success(
      new GetUserSessionsQueryResult(data.Select(s => s.ToDto(now)).ToList()));
  }
}
