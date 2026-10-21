using Microsoft.EntityFrameworkCore;

public class SetAuthorizedOfficerHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<SetAuthorizedOfficerCommand, Result<SetAuthorizedOfficerCommandResult>>
{
  public async Task<Result<SetAuthorizedOfficerCommandResult>> Handle(SetAuthorizedOfficerCommand command, CancellationToken cancellationToken)
  {
    var id = UserId.Of(command.Id);

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found - it may have been deleted.");

    // a legal designation is a privilege: nobody grants it to themselves
    guard.EnsureNotSelf(id, "change the authorized-officer designation of");
    await guard.EnsureCanActOnAsync(id, "change the authorized-officer designation of", cancellationToken);

    // setting what is already set records nothing
    if (user.IsAuthorizedOfficer == command.IsAuthorizedOfficer)
      return Result<SetAuthorizedOfficerCommandResult>.Success(new SetAuthorizedOfficerCommandResult(user.IsAuthorizedOfficer));

    user.SetAuthorizedOfficer(command.IsAuthorizedOfficer);
    await activity.RecordAsync(command.IsAuthorizedOfficer ? ActivityAction.AuthorizedOfficerSet : ActivityAction.AuthorizedOfficerCleared,
      ActivityTargetType.User, user.Id.Value, user.Username.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<SetAuthorizedOfficerCommandResult>.Success(new SetAuthorizedOfficerCommandResult(user.IsAuthorizedOfficer));
  }
}
