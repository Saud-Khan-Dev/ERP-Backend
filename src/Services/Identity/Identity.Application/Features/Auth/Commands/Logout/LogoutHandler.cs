using Microsoft.EntityFrameworkCore;

public class LogoutHandler(IApplicationDbContext context, ITokenService tokenService)
  : ICommandHandler<LogoutCommand, Result<LogoutCommandResult>>
{
  public async Task<Result<LogoutCommandResult>> Handle(LogoutCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var hash = tokenService.HashRefreshToken(command.RefreshToken);

    var session = await context.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, cancellationToken);

    // unknown or already-dead token: logout is idempotent and must not confirm whether it existed
    if (session is null)
      return Result<LogoutCommandResult>.Success(new LogoutCommandResult(true));

    if (command.AllSessions)
    {
      var sessions = await context.Sessions
          .Where(s => s.UserId == session.UserId && s.RevokedAt == null)
          .ToListAsync(cancellationToken);

      foreach (var active in sessions)
        active.Revoke(now, "Logout (all devices)");
    }
    else
    {
      session.Revoke(now, "Logout");
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<LogoutCommandResult>.Success(new LogoutCommandResult(true));
  }
}
