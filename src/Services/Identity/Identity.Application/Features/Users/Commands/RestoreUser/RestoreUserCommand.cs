using Microsoft.EntityFrameworkCore;

/// Brings a deleted account back - switched off, so an administrator reviews its roles before activating it.
public sealed record RestoreUserCommand(Guid Id) : ICommand<Result<RestoreUserCommandResult>>;

public sealed record RestoreUserCommandResult(bool IsSuccess);

public class RestoreUserHandler(IApplicationDbContext context, IdentityGuard guard, IActivityRecorder activity)
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

    // its username and email were freed when it was deleted: refused while another account in use has taken one
    var taken = await context.Users.AsNoTracking().Where(u => u.Id != id && (u.Username == user.Username || u.Email == user.Email))
      .Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    if (taken is not null)
      return Result<RestoreUserCommandResult>.Failure($"The account {taken} now uses this username or email - this one cannot be restored. Register a new account instead.");
    var code = user.EmployeeCode;
    if (user.EmployeeId is not null && code is not null && await context.Users.AnyAsync(u => u.Id != id && u.EmployeeCode == code && u.EmployeeId != null, cancellationToken))
      return Result<RestoreUserCommandResult>.Failure($"Another account in use is linked to {code.Value} - this one cannot be restored.");

    user.Restore();
    await activity.RecordAsync(ActivityAction.UserRestored, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value, null, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Result<RestoreUserCommandResult>.Success(new RestoreUserCommandResult(true));
  }
}
