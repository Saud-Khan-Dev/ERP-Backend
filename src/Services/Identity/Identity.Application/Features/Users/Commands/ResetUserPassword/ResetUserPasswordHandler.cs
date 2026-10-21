using Microsoft.EntityFrameworkCore;

public class ResetUserPasswordHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    ISecuritySettingsProvider securitySettings,
    IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<ResetUserPasswordCommand, Result<ResetUserPasswordCommandResult>>
{
  public async Task<Result<ResetUserPasswordCommandResult>> Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var policy = (await securitySettings.GetAsync(cancellationToken)).PasswordPolicy;

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(command.Id), cancellationToken)
      ?? throw new UserNotFoundException("This account was not found.");

    // your own password is changed under My account, where the old one is asked for
    guard.EnsureNotSelf(user.Id, "reset the password of");
    await guard.EnsureCanActOnAsync(user.Id, "reset the password of", cancellationToken);

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

    await activity.RecordAsync(ActivityAction.PasswordReset, ActivityTargetType.User, user.Id.Value, user.Username.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<ResetUserPasswordCommandResult>.Success(
      new ResetUserPasswordCommandResult(generated ? password : null));
  }
}
