using Microsoft.EntityFrameworkCore;

/// Exchanges a refresh token for a new access token, rotating the refresh token as it goes.
///
/// Rotation gives us theft detection: each session records the session that replaced it, so a
/// refresh token presented after it was already rotated means the token leaked — and the whole
/// chain is revoked rather than served.
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

    // presented after rotation => the token was captured; burn every session in the chain
    if (session.ReplacedBySessionId is not null)
    {
      await RevokeAllSessionsAsync(session.UserId, now, "Refresh token reuse detected", cancellationToken);
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

    var newSessionId = SessionId.Of(Guid.NewGuid());
    var refresh = tokenService.CreateRefreshToken(security.RefreshTokenLifetime);
    var replacement = Session.Create(newSessionId, user.Id, refresh.Hash, ip, command.UserAgent, now, refresh.ExpiresAt);

    session.RotateTo(newSessionId, now);

    var access = tokenService.CreateAccessToken(
      user, newSessionId,
      resolved.PermissionCodes.ToArray(),
      resolved.RoleCodes.ToArray(),
      security.AccessTokenLifetime);

    await context.Sessions.AddAsync(replacement, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RefreshTokenCommandResult>.Success(new RefreshTokenCommandResult(new AuthenticationResultDto(
      access.Value,
      access.ExpiresAt,
      refresh.Value,
      refresh.ExpiresAt,
      user.MustChangePassword,
      user.ToCurrentUserDto(resolved))));
  }

  private async Task RevokeAllSessionsAsync(UserId userId, DateTime now, string reason, CancellationToken cancellationToken)
  {
    var sessions = await context.Sessions
        .Where(s => s.UserId == userId && s.RevokedAt == null)
        .ToListAsync(cancellationToken);

    foreach (var session in sessions)
      session.Revoke(now, reason);

    await context.SaveChangesAsync(cancellationToken);
  }
}
