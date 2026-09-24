/// Grants a role to a user, optionally for a limited time.
///
/// Uses a surrogate key rather than (user_id, role_id): with a composite key a revoked grant could
/// never be re-issued without destroying the revocation record. A partial unique index keeps only
/// one *live* grant per (user, role).
public class UserRole : Entity<UserRoleId>
{
  public UserId UserId { get; private set; } = default!;
  public RoleId RoleId { get; private set; } = default!;
  public DateTime AssignedAt { get; private set; }
  public Guid? AssignedBy { get; private set; }
  /// Optional — for temporary access such as covering a colleague's leave.
  public DateTime? ExpiresAt { get; private set; }
  public DateTime? RevokedAt { get; private set; }

  public bool IsLive(DateTime now) =>
      RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

  public static UserRole Create(UserId userId, RoleId roleId, Guid? assignedBy, DateTime? expiresAt, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(userId);
    ArgumentNullException.ThrowIfNull(roleId);

    if (expiresAt.HasValue && expiresAt.Value <= now)
      throw new DomainException("A role grant cannot expire in the past.");

    return new UserRole
    {
      Id = UserRoleId.Of(Guid.NewGuid()),
      UserId = userId,
      RoleId = roleId,
      AssignedAt = now,
      AssignedBy = assignedBy,
      ExpiresAt = expiresAt
    };
  }

  public void Revoke(DateTime now)
  {
    if (RevokedAt.HasValue)
      throw new DomainException("This role grant has already been revoked.");

    RevokedAt = now;
  }
}
