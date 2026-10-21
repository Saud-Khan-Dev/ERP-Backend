using Microsoft.EntityFrameworkCore;

public class UnlockUserHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<UnlockUserCommand, Result<UnlockUserCommandResult>>
{
  public async Task<Result<UnlockUserCommandResult>> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
  {
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(command.Id), cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");

    await guard.EnsureCanActOnAsync(user.Id, "unlock", cancellationToken);

    // unlocking an account that is not locked (and has no wrong attempts counting) changes nothing - nothing to record
    if (!user.IsLockedOut(DateTime.UtcNow) && user.FailedLoginAttempts == 0)
      return Result<UnlockUserCommandResult>.Success(new UnlockUserCommandResult(true));

    user.Unlock();
    await activity.RecordAsync(ActivityAction.UserUnlocked, ActivityTargetType.User, user.Id.Value, user.Username.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UnlockUserCommandResult>.Success(new UnlockUserCommandResult(true));
  }
}
