using Microsoft.EntityFrameworkCore;

/// Exchanges a refresh token for a new access token, rotating the refresh token as it goes.
///
/// Rotation gives us theft detection: each session records the session that replaced it, so a
/// refresh token presented after it was already rotated means the token leaked — and that sign-in's chain is revoked
/// (the person's other devices stay signed in). A second use within 30 seconds of the rotation is a lost answer or a
/// double click, not theft: it is refused without revoking anything.
public class RefreshTokenHandler(
    IApplicationDbContext context,
    ITokenService tokenService,
    IUserPermissionService permissionService,
    ISecuritySettingsProvider securitySettings)
  : ICommandHandler<RefreshTokenCommand, Result<RefreshTokenCommandResult>>
{
  public async Task<Result<RefreshTokenCommandResult>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var security = await securitySettings.GetAsync(cancellationToken);
    var hash = tokenService.HashRefreshToken(command.RefreshToken);

    var session = await context.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, cancellationToken)
      ?? throw new InvalidCredentialsException("Unknown refresh token");

    if (session.ReplacedBySessionId is not null)
    {
      if (session.RevokedAt is { } rotatedAt && now - rotatedAt < ReuseGrace)
        throw new InvalidCredentialsException("Refresh token already used");

      // presented after rotation => the token was captured; burn this sign-in's chain
      await RevokeChainAsync(session, now, "Refresh token reuse detected", cancellationToken);
      throw new InvalidCredentialsException("Refresh token reuse detected");
    }

    if (!session.IsActive(now))
      throw new InvalidCredentialsException(session.IsRevoked ? "Session revoked" : "Session expired");

    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == session.UserId, cancellationToken)
      ?? throw new InvalidCredentialsException("Session belongs to a missing user");

    // an account disabled or locked since the token was issued must not be able to refresh
    user.EnsureCanSignIn(now);

    var ip = IpAddress.OfNullable(command.IpAddress);
    var resolved = await permissionService.ResolveAsync(user.Id, cancellationToken);
    var roleNames = await permissionService.RoleNamesAsync(resolved.RoleCodes, cancellationToken);

    var newSessionId = SessionId.Of(Guid.NewGuid());
    var refresh = tokenService.CreateRefreshToken(security.RefreshTokenLifetime);
    var replacement = Session.Create(newSessionId, user.Id, refresh.Hash, ip, command.UserAgent, now, refresh.ExpiresAt);

    session.RotateTo(newSessionId, now);

    var access = tokenService.CreateAccessToken(
      user, newSessionId,
      user.MustChangePassword ? [] : resolved.PermissionCodes.ToArray(),
      user.MustChangePassword ? [] : resolved.RoleCodes.ToArray(),
      security.AccessTokenLifetime);

    await context.Sessions.AddAsync(replacement, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RefreshTokenCommandResult>.Success(new RefreshTokenCommandResult(new AuthenticationResultDto(
      access.Value,
      access.ExpiresAt,
      refresh.Value,
      refresh.ExpiresAt,
      user.MustChangePassword,
      user.ToCurrentUserDto(resolved, roleNames))));
  }

  private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

  /// Revokes every session that grew from this one by rotation (its successor, the successor's successor ...).
  private async Task RevokeChainAsync(Session reused, DateTime now, string reason, CancellationToken cancellationToken)
  {
    var sessions = await context.Sessions.Where(s => s.UserId == reused.UserId).ToDictionaryAsync(s => s.Id, cancellationToken);
    var next = reused.ReplacedBySessionId;
    for (var guard = 0; next is not null && guard < 10_000 && sessions.TryGetValue(next, out var successor); guard++)
    {
      successor.Revoke(now, reason);
      next = successor.ReplacedBySessionId;
    }
    await context.SaveChangesAsync(cancellationToken);
  }
}
