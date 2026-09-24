using Microsoft.EntityFrameworkCore;

public class RevokeUserSessionsHandler(IApplicationDbContext context)
  : ICommandHandler<RevokeUserSessionsCommand, Result<RevokeUserSessionsCommandResult>>
{
  public async Task<Result<RevokeUserSessionsCommandResult>> Handle(RevokeUserSessionsCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(command.UserId);

    if (!await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException($"User {command.UserId} was not found.");

    var sessions = await context.Sessions
        .Where(s => s.UserId == userId && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, "Revoked by administrator");

    await context.SaveChangesAsync(cancellationToken);

    return Result<RevokeUserSessionsCommandResult>.Success(new RevokeUserSessionsCommandResult(sessions.Count));
  }
}
