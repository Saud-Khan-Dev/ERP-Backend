using Microsoft.EntityFrameworkCore;

public class SetAuthorizedOfficerHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<SetAuthorizedOfficerCommand, Result<SetAuthorizedOfficerCommandResult>>
{
  public async Task<Result<SetAuthorizedOfficerCommandResult>> Handle(SetAuthorizedOfficerCommand command, CancellationToken cancellationToken)
  {
    var id = UserId.Of(command.Id);

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    // a legal designation is a privilege: nobody grants it to themselves
    guard.EnsureNotSelf(id, "change your own authorized-officer designation");

    user.SetAuthorizedOfficer(command.IsAuthorizedOfficer);
    await activity.RecordAsync(command.IsAuthorizedOfficer ? ActivityAction.AuthorizedOfficerSet : ActivityAction.AuthorizedOfficerCleared,
      ActivityTargetType.User, user.Id.Value, user.Username.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<SetAuthorizedOfficerCommandResult>.Success(new SetAuthorizedOfficerCommandResult(user.IsAuthorizedOfficer));
  }
}
