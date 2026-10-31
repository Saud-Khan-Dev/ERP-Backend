using Microsoft.EntityFrameworkCore;

public class UpdateUserHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<UpdateUserCommand, Result<UpdateUserCommandResult>>
{
  public async Task<Result<UpdateUserCommandResult>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
  {
    var id = UserId.Of(command.Id);
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");

    await guard.EnsureCanActOnAsync(id, "edit", cancellationToken);

    var input = command.User;
    var email = EmailAddress.Of(input.Email);

    if (await context.Users.AnyAsync(u => u.Id != id && u.Email == email, cancellationToken))
      return Result<UpdateUserCommandResult>.Failure($"Email '{email.Value}' is already registered.");

    var before = (Name: user.DisplayName.Value, Email: user.Email.Value);

    user.UpdateProfile(email, Name.Of(input.DisplayName));

    var detail = ActivityDetail.Changes(
      ("Name", before.Name, user.DisplayName.Value),
      ("Email", before.Email, user.Email.Value));
    if (detail is not null)
      await activity.RecordAsync(ActivityAction.UserProfileUpdated, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value, detail, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateUserCommandResult>.Success(new UpdateUserCommandResult(true));
  }
}
