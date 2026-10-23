using Microsoft.EntityFrameworkCore;

/// "Sign out my other devices": every session of the caller but the one this request came from.
public sealed record RevokeOtherSessionsCommand : ICommand<Result<RevokeOtherSessionsCommandResult>>;

public sealed record RevokeOtherSessionsCommandResult(int RevokedCount);

public class RevokeOtherSessionsHandler(IApplicationDbContext context, ICurrentUser currentUser, IActivityRecorder activity)
  : ICommandHandler<RevokeOtherSessionsCommand, Result<RevokeOtherSessionsCommandResult>>
{
  public async Task<Result<RevokeOtherSessionsCommandResult>> Handle(RevokeOtherSessionsCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(currentUser.UserId ?? throw new InvalidCredentialsException("No authenticated user"));
    var current = currentUser.SessionId is { } sid ? SessionId.Of(sid) : null;

    var sessions = await context.Sessions
      .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now && (current == null || s.Id != current))
      .ToListAsync(cancellationToken);
    foreach (var session in sessions)
      session.Revoke(now, "Signed out from another device");
    if (sessions.Count > 0)
      await activity.RecordAsync(ActivityAction.SignedOutOtherDevices, ActivityTargetType.User, userId.Value, null,
        $"{sessions.Count} other session{(sessions.Count == 1 ? "" : "s")}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<RevokeOtherSessionsCommandResult>.Success(new RevokeOtherSessionsCommandResult(sessions.Count));
  }
}
