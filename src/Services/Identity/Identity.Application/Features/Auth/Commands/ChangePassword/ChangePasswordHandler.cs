using Microsoft.EntityFrameworkCore;

public class ChangePasswordHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    ISecuritySettingsProvider securitySettings,
    IActivityRecorder activity)
  : ICommandHandler<ChangePasswordCommand, Result<ChangePasswordCommandResult>>
{
  public async Task<Result<ChangePasswordCommandResult>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var userId = currentUser.UserId
      ?? throw new InvalidCredentialsException("No authenticated user");

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == UserId.Of(userId), cancellationToken)
      ?? throw new UserNotFoundException("This account was not found - it may have been deleted.");

    var security = await securitySettings.GetAsync(cancellationToken);
    if (passwordHasher.Verify(user.PasswordHash.Value, command.CurrentPassword) == PasswordVerificationOutcome.Failed)
    {
      // guessing the current password here counts like guessing it at sign-in
      var locked = user.RegisterFailedLogin(security.MaxFailedLoginAttempts, security.LockoutDuration, now);
      if (locked)
      {
        foreach (var live in await context.Sessions.Where(s => s.UserId == user.Id && s.RevokedAt == null).ToListAsync(cancellationToken))
          live.Revoke(now, "Account locked");
        await activity.RecordAsync(ActivityAction.AccountLockedByPasswordChange, ActivityTargetType.User, user.Id.Value, user.Username.Value,
          $"Wrong current passwords on Change password - locked for {security.LockoutMinutes} minutes, signed out everywhere", cancellationToken);
      }
      await context.SaveChangesAsync(cancellationToken);
      return Result<ChangePasswordCommandResult>.Failure(locked
        ? $"Your current password is incorrect. Too many wrong passwords: the account is locked for {security.LockoutMinutes} minutes."
        : "Your current password is incorrect.");
    }

    var policy = security.PasswordPolicy;
    PasswordPolicy.Validate(command.NewPassword, policy);

    user.SetPassword(PasswordHash.Of(passwordHasher.Hash(command.NewPassword)), mustChangePassword: false, now);

    // a password change invalidates every existing session: anyone who had the old credentials is out
    var sessions = await context.Sessions
        .Where(s => s.UserId == user.Id && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, "Password changed");

    await activity.RecordAsync(ActivityAction.PasswordChanged, ActivityTargetType.User, user.Id.Value, user.Username.Value,
      sessions.Count > 0 ? $"Signed out of {sessions.Count} session{(sessions.Count == 1 ? "" : "s")}" : null, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ChangePasswordCommandResult>.Success(new ChangePasswordCommandResult(true));
  }
}
