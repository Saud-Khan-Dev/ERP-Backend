using Microsoft.EntityFrameworkCore;

/// Brings a deleted account back within the recovery period - switched off, so an administrator reviews its roles before
/// activating it. After the period the account is closed for good.
public sealed record RestoreUserCommand(Guid Id) : ICommand<Result<RestoreUserCommandResult>>;

public sealed record RestoreUserCommandResult(bool IsSuccess);

public class RestoreUserHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity, ISecuritySettingsProvider settings)
  : ICommandHandler<RestoreUserCommand, Result<RestoreUserCommandResult>>
{
  public async Task<Result<RestoreUserCommandResult>> Handle(RestoreUserCommand command, CancellationToken cancellationToken)
  {
    var id = UserId.Of(command.Id);
    var user = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");
    await guard.EnsureCanActOnAsync(id, "restore", cancellationToken);

    if (!user.IsDeleted)
      return Result<RestoreUserCommandResult>.Success(new RestoreUserCommandResult(true));

    user.Restore(DateTime.UtcNow, (await settings.GetAsync(cancellationToken)).DeletedRecoveryDays);
    await activity.RecordAsync(ActivityAction.UserRestored, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value, null, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<RestoreUserCommandResult>.Success(new RestoreUserCommandResult(true));
  }
}
