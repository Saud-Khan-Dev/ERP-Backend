using Microsoft.EntityFrameworkCore;

public class UpdateUserHandler(IApplicationDbContext context, EmployeeCodeService employeeCodes)
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

    user.UpdateProfile(email, Name.Of(input.DisplayName), input.EmployeeId, employeeCode.Value);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateUserCommandResult>.Success(new UpdateUserCommandResult(true));
  }
}
