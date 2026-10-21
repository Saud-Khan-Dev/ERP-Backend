using Microsoft.EntityFrameworkCore;

/// Sign-in.
///
/// Order matters: the password is verified *before* the account state is checked, so that a locked
/// or disabled account cannot be used to discover which usernames exist - and an unknown username is checked against a
/// dummy hash, so it takes as long as a known one. Every outcome — including "no such user" — is written to
/// login_attempt with its specific reason.
///
/// An account still holding a temporary password gets a token with no permissions and no roles (claim `pwd`): it opens
/// only the person's own /auth routes until the password is changed.
public class LoginHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUserPermissionService permissionService,
    ISecuritySettingsProvider securitySettings)
  : ICommandHandler<LoginCommand, Result<LoginCommandResult>>
{
  public async Task<Result<LoginCommandResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var security = await securitySettings.GetAsync(cancellationToken);
    var ip = IpAddress.OfNullable(command.IpAddress);
    var attempted = command.Username?.Trim().ToLowerInvariant() ?? string.Empty;

    var user = await FindUserAsync(attempted, cancellationToken);

    if (user is null)
    {
      passwordHasher.Verify(DummyHash(), command.Password ?? string.Empty);
      await FailAsync(null, attempted, "Unknown username", ip, command.UserAgent, now, cancellationToken);
    }

    var verification = passwordHasher.Verify(user!.PasswordHash.Value, command.Password);

    if (verification == PasswordVerificationOutcome.Failed)
    {
      var lockedOutNow = !user.IsLockedOut(now)
          && user.RegisterFailedLogin(security.MaxFailedLoginAttempts, security.LockoutDuration, now);

      await FailAsync(
        user.Id,
        attempted,
        lockedOutNow ? "Wrong password — account locked" : "Wrong password",
        ip, command.UserAgent, now, cancellationToken);
    }

    // password is right; now decide whether this account may sign in at all
    try
    {
      user.EnsureCanSignIn(now);
    }
    catch (AccountUnavailableException)
    {
      var reason = user.IsDeleted ? "Account deleted" : !user.IsActive ? "Account deactivated" : "Account locked";
      await RecordAttemptAsync(
        LoginAttempt.Failure(user.Id, attempted, reason, ip, command.UserAgent, now),
        cancellationToken);
      throw;
    }

    // correct password stored with outdated hashing parameters — upgrade it silently
    if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
      user.SetPassword(PasswordHash.Of(passwordHasher.Hash(command.Password)), user.MustChangePassword, now);

    var resolved = await permissionService.ResolveAsync(user.Id, cancellationToken);
    var roleNames = await permissionService.RoleNamesAsync(resolved.RoleCodes, cancellationToken);

    var sessionId = SessionId.Of(Guid.NewGuid());
    var refresh = tokenService.CreateRefreshToken(security.RefreshTokenLifetime);
    var session = Session.Create(sessionId, user.Id, refresh.Hash, ip, command.UserAgent, now, refresh.ExpiresAt);

    // a temporary password opens nothing but the change-password screen
    var access = tokenService.CreateAccessToken(
      user, sessionId,
      user.MustChangePassword ? [] : resolved.PermissionCodes.ToArray(),
      user.MustChangePassword ? [] : resolved.RoleCodes.ToArray(),
      security.AccessTokenLifetime);

    // sessions that ended long ago are only clutter (every refresh rotates one): keep a month of them
    var cutoff = now.AddDays(-30);
    var stale = await context.Sessions
      .Where(s => s.UserId == user.Id && ((s.RevokedAt != null && s.RevokedAt < cutoff) || s.ExpiresAt < cutoff))
      .ToListAsync(cancellationToken);
    context.Sessions.RemoveRange(stale);

    user.RegisterSuccessfulLogin(ip, now);

    await context.Sessions.AddAsync(session, cancellationToken);
    await context.LoginAttempts.AddAsync(
      LoginAttempt.Success(user.Id, attempted, ip, command.UserAgent, now), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<LoginCommandResult>.Success(new LoginCommandResult(new AuthenticationResultDto(
      access.Value,
      access.ExpiresAt,
      refresh.Value,
      refresh.ExpiresAt,
      user.MustChangePassword,
      user.ToCurrentUserDto(resolved, roleNames))));
  }

  private static string? _dummyHash;

  /// A hash of nothing anyone knows, verified for unknown usernames so they take as long as known ones.
  private string DummyHash() => _dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString("N"));

  /// Accepts either the username or the email address.
  private async Task<User?> FindUserAsync(string attempted, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(attempted))
      return null;

    Username? username = null;
    EmailAddress? email = null;

    try { username = Username.Of(attempted); } catch (DomainException) { }
    try { email = EmailAddress.Of(attempted); } catch (DomainException) { }

    if (username is null && email is null)
      return null;

    return await context.Users
        .FirstOrDefaultAsync(u => (username != null && u.Username == username)
                               || (email != null && u.Email == email), cancellationToken);
  }

  /// Persists the failed attempt (and any lockout it caused) before reporting a generic failure.
  private async Task FailAsync(
      UserId? userId, string attempted, string reason,
      IpAddress? ip, string? userAgent, DateTime now, CancellationToken cancellationToken)
  {
    await RecordAttemptAsync(
      LoginAttempt.Failure(userId, attempted, reason, ip, userAgent, now), cancellationToken);

    throw new InvalidCredentialsException(reason);
  }

  private async Task RecordAttemptAsync(LoginAttempt attempt, CancellationToken cancellationToken)
  {
    await context.LoginAttempts.AddAsync(attempt, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
  }
}
