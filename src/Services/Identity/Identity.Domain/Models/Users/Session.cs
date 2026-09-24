/// One refresh-token lifetime. Storing sessions is what makes forced logout, revocation and
/// "which devices am I signed in on" possible.
///
/// The refresh token itself is never stored — only a hash of it, so a database leak does not hand
/// out valid tokens.
public class Session : Aggregate<SessionId>
{
  public UserId UserId { get; private set; } = default!;
  public string RefreshTokenHash { get; private set; } = default!;
  public IpAddress? IpAddress { get; private set; }
  public string? UserAgent { get; private set; }
  public DateTime IssuedAt { get; private set; }
  public DateTime ExpiresAt { get; private set; }
  public DateTime? RevokedAt { get; private set; }
  public string? RevokedReason { get; private set; }

  /// Set when this session was rotated: the session that replaced it. Lets us spot reuse of an
  /// already-rotated refresh token, which means the token was stolen.
  public SessionId? ReplacedBySessionId { get; private set; }

  public bool IsRevoked => RevokedAt.HasValue;

  public bool IsActive(DateTime now) => !IsRevoked && ExpiresAt > now;

  public static Session Create(
      SessionId id,
      UserId userId,
      string refreshTokenHash,
      IpAddress? ipAddress,
      string? userAgent,
      DateTime issuedAt,
      DateTime expiresAt)
  {
    ArgumentNullException.ThrowIfNull(userId);

    if (string.IsNullOrWhiteSpace(refreshTokenHash))
      throw new DomainException("A refresh token hash is required.");

    if (expiresAt <= issuedAt)
      throw new DomainException("A session must expire after it is issued.");

    return new Session
    {
      Id = id,
      UserId = userId,
      RefreshTokenHash = refreshTokenHash,
      IpAddress = ipAddress,
      UserAgent = Truncate(userAgent),
      IssuedAt = issuedAt,
      ExpiresAt = expiresAt
    };
  }

  public void Revoke(DateTime now, string reason)
  {
    if (IsRevoked)
      return;

    RevokedAt = now;
    RevokedReason = reason;
  }

  /// Marks this session as replaced by a newly issued one during refresh-token rotation.
  public void RotateTo(SessionId replacement, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(replacement);

    if (IsRevoked)
      throw new DomainException("A revoked session cannot be rotated.");

    ReplacedBySessionId = replacement;
    Revoke(now, "Rotated");
  }

  private static string? Truncate(string? userAgent) =>
      string.IsNullOrWhiteSpace(userAgent) ? null
        : userAgent.Length > 512 ? userAgent[..512]
        : userAgent;
}
