using Microsoft.EntityFrameworkCore;

public class GetMySessionsHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : IQueryHandler<GetMySessionsQuery, Result<GetMySessionsQueryResult>>
{
  public async Task<Result<GetMySessionsQueryResult>> Handle(GetMySessionsQuery query, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var userId = UserId.Of(currentUser.UserId
      ?? throw new InvalidCredentialsException("No authenticated user"));

    var sessions = context.Sessions.AsNoTracking().Where(s => s.UserId == userId);

    if (!query.IncludeRevoked)
      sessions = sessions.Where(s => s.RevokedAt == null && s.ExpiresAt > now);

    var data = await sessions.OrderByDescending(s => s.IssuedAt).ToListAsync(cancellationToken);

    return Result<GetMySessionsQueryResult>.Success(
      new GetMySessionsQueryResult(data.Select(s => s.ToDto(now)).ToList()));
  }
}
