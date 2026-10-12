using Microsoft.EntityFrameworkCore;

public class UpdateUserHandler(IApplicationDbContext context, EmployeeCodeService employeeCodes,
    IActivityRecorder activity)
  : ICommandHandler<UpdateUserCommand, Result<UpdateUserCommandResult>>
{
  public async Task<Result<UpdateUserCommandResult>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
  {
    var id = UserId.Of(command.Id);
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    var input = command.User;
    var email = EmailAddress.Of(input.Email);

    if (await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != id && u.Email == email, cancellationToken))
      return Result<UpdateUserCommandResult>.Failure($"Email '{email.Value}' is already registered.");

    if (input.EmployeeId is { } employeeId && employeeId != Guid.Empty
        && await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != id && u.EmployeeId == employeeId, cancellationToken))
      return Result<UpdateUserCommandResult>.Failure("That employee already has a login account.");

    var employeeCode = await employeeCodes.ForExistingUserAsync(user, input.EmployeeCode, cancellationToken);
    if (!employeeCode.IsSuccess)
      return Result<UpdateUserCommandResult>.Failure(employeeCode.Message!);

    var before = (Name: user.DisplayName.Value, Email: user.Email.Value, Code: user.EmployeeCode?.Value, user.EmployeeId);

    user.UpdateProfile(email, Name.Of(input.DisplayName), input.EmployeeId, employeeCode.Value);

    var detail = ActivityDetail.Changes(
      ("Name", before.Name, user.DisplayName.Value),
      ("Email", before.Email, user.Email.Value),
      ("Employee code", before.Code, user.EmployeeCode?.Value),
      ("Employee link", before.EmployeeId?.ToString(), user.EmployeeId?.ToString()));
    if (detail is not null)
      await activity.RecordAsync(ActivityAction.UserProfileUpdated, ActivityTargetType.User, user.Id.Value, user.Username.Value, detail, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateUserCommandResult>.Success(new UpdateUserCommandResult(true));
  }
}
