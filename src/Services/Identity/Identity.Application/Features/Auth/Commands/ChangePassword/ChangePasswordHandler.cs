using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class ChangePasswordHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IOptions<SecurityOptions> securityOptions)
  : ICommandHandler<ChangePasswordCommand, Result<ChangePasswordCommandResult>>
{
  public async Task<Result<ChangePasswordCommandResult>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var userId = currentUser.UserId
      ?? throw new InvalidCredentialsException("No authenticated user");

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(userId), cancellationToken)
      ?? throw new UserNotFoundException($"User {userId} was not found.");

    if (passwordHasher.Verify(user.PasswordHash.Value, command.CurrentPassword) == PasswordVerificationOutcome.Failed)
      return Result<ChangePasswordCommandResult>.Failure("Your current password is incorrect.");

    PasswordPolicy.Validate(command.NewPassword, securityOptions.Value.PasswordPolicy);

    user.SetPassword(PasswordHash.Of(passwordHasher.Hash(command.NewPassword)), mustChangePassword: false, now);

    // a password change invalidates every existing session: anyone who had the old credentials is out
    var sessions = await context.Sessions
        .Where(s => s.UserId == user.Id && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, "Password changed");

    await context.SaveChangesAsync(cancellationToken);

    return Result<ChangePasswordCommandResult>.Success(new ChangePasswordCommandResult(true));
  }
}
