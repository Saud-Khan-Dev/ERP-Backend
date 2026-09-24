/// A per-user exception to the role model: grant one extra permission, or withhold one the user's
/// role would otherwise give.
///
/// The key is (user_id, permission_id), so ALLOW and DENY are mutually exclusive for the same
/// permission — which is what we want. DENY always wins over a role grant; see PermissionResolver.
public class UserPermissionOverride : Entity<Guid>
{
  public UserId UserId { get; private set; } = default!;
  public PermissionId PermissionId { get; private set; } = default!;
  public OverrideEffect Effect { get; private set; }
  public DateTime GrantedAt { get; private set; }
  public Guid? GrantedBy { get; private set; }
  public DateTime? ExpiresAt { get; private set; }
  public string? Reason { get; private set; }

  public bool IsLive(DateTime now) => ExpiresAt is null || ExpiresAt > now;

  public static UserPermissionOverride Create(
      UserId userId,
      PermissionId permissionId,
      OverrideEffect effect,
      Guid? grantedBy,
      DateTime? expiresAt,
      string? reason,
      DateTime now)
  {
    ArgumentNullException.ThrowIfNull(userId);
    ArgumentNullException.ThrowIfNull(permissionId);

    if (expiresAt.HasValue && expiresAt.Value <= now)
      throw new DomainException("An override cannot expire in the past.");

    return new UserPermissionOverride
    {
      UserId = userId,
      PermissionId = permissionId,
      Effect = effect,
      GrantedAt = now,
      GrantedBy = grantedBy,
      ExpiresAt = expiresAt,
      Reason = reason
    };
  }

  public void Update(OverrideEffect effect, DateTime? expiresAt, string? reason, DateTime now)
  {
    if (expiresAt.HasValue && expiresAt.Value <= now)
      throw new DomainException("An override cannot expire in the past.");

    Effect = effect;
    ExpiresAt = expiresAt;
    Reason = reason;
  }
}
