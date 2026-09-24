using Microsoft.EntityFrameworkCore;

public class RevokeMySessionHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<RevokeMySessionCommand, Result<RevokeMySessionCommandResult>>
{
  public async Task<Result<RevokeMySessionCommandResult>> Handle(RevokeMySessionCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var userId = UserId.Of(currentUser.UserId
      ?? throw new InvalidCredentialsException("No authenticated user"));

    var sessionId = SessionId.Of(command.SessionId);

    // scoped to the caller's own sessions: you cannot revoke someone else's by guessing an id
    var session = await context.Sessions
        .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken)
      ?? throw new SessionNotFoundException($"Session {command.SessionId} was not found.");

    session.Revoke(now, "Revoked by user");
    await context.SaveChangesAsync(cancellationToken);

    return Result<RevokeMySessionCommandResult>.Success(new RevokeMySessionCommandResult(true));
  }
}
