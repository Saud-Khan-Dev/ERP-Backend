using Microsoft.EntityFrameworkCore;

public class UpdateUserHandler(IApplicationDbContext context)
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

    user.UpdateProfile(email, Name.Of(input.DisplayName), input.EmployeeId);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateUserCommandResult>.Success(new UpdateUserCommandResult(true));
  }
}
