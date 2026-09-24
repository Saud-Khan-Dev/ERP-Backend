using Microsoft.EntityFrameworkCore;

public class SetUserActivationHandler(IApplicationDbContext context, IdentityGuard guard)
  : ICommandHandler<SetUserActivationCommand, Result<SetUserActivationCommandResult>>
{
  public async Task<Result<SetUserActivationCommandResult>> Handle(SetUserActivationCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = UserId.Of(command.Id);

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    if (!command.IsActive)
    {
      guard.EnsureNotSelf(id, "deactivate your own account");
      await guard.EnsureNotLastSuperAdminAsync(id, "deactivating it", cancellationToken);

      user.Deactivate();

      var sessions = await context.Sessions
          .Where(s => s.UserId == id && s.RevokedAt == null)
          .ToListAsync(cancellationToken);

      foreach (var session in sessions)
        session.Revoke(now, "Account deactivated");
    }
    else
    {
      user.Activate();
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<SetUserActivationCommandResult>.Success(new SetUserActivationCommandResult(user.IsActive));
  }
}
