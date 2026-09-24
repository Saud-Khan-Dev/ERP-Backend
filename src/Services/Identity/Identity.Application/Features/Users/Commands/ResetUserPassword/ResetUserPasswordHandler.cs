using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public class ResetUserPasswordHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    IOptions<SecurityOptions> securityOptions)
  : ICommandHandler<ResetUserPasswordCommand, Result<ResetUserPasswordCommandResult>>
{
  public async Task<Result<ResetUserPasswordCommandResult>> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var policy = securityOptions.Value.PasswordPolicy;

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(command.Id), cancellationToken)
      ?? throw new UserNotFoundException($"User {command.Id} was not found.");

    var generated = command.NewPassword is null;
    var password = command.NewPassword ?? passwordGenerator.Generate(policy);
    PasswordPolicy.Validate(password, policy);

    user.SetPassword(PasswordHash.Of(passwordHasher.Hash(password)), mustChangePassword: true, now);

    // whoever was signed in with the old password is signed out
    var sessions = await context.Sessions
        .Where(s => s.UserId == user.Id && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, "Password reset by administrator");

    await context.SaveChangesAsync(cancellationToken);

    return Result<ResetUserPasswordCommandResult>.Success(
      new ResetUserPasswordCommandResult(generated ? password : null));
  }
}
