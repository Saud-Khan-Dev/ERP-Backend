using Microsoft.EntityFrameworkCore;

public class UnlockUserHandler(IApplicationDbContext context)
  : ICommandHandler<UnlockUserCommand, Result<UnlockUserCommandResult>>
{
  public async Task<Result<UnlockUserCommandResult>> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
  {
    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(command.Id), cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    user.Unlock();
    await context.SaveChangesAsync(cancellationToken);

    return Result<UnlockUserCommandResult>.Success(new UnlockUserCommandResult(true));
  }
}
