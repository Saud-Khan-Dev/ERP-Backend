public enum DeletionRequestStatus { Pending, Approved, Rejected, Withdrawn }

/// A person asks for their own account to be deleted - nobody deletes their own account. A senior with the right to
/// delete accounts (IAM_USERS.DELETE, never the person themself) approves it, and the account is deleted (soft: it can be
/// restored within the recovery period), or turns it down with the reason. The person may withdraw it while it waits.
public class AccountDeletionRequest : Entity<Guid>
{
  public UserId UserId { get; private set; } = default!;
  public string UserName { get; private set; } = default!;
  public string Reason { get; private set; } = default!;
  public DateTime RequestedAt { get; private set; }
  public DeletionRequestStatus Status { get; private set; }
  public Guid? DecidedById { get; private set; }
  public string? DecidedByName { get; private set; }
  public DateTime? DecidedAt { get; private set; }
  public string? Remarks { get; private set; }

  public bool IsPending => Status == DeletionRequestStatus.Pending;

  public static AccountDeletionRequest Ask(User user, string? reason, DateTime now)
  {
    ArgumentNullException.ThrowIfNull(user);
    if (user.IsDeleted)
      throw new DomainException("This account is already deleted.");
    var why = reason?.Trim();
    if (string.IsNullOrEmpty(why))
      throw new DomainException("Say why the account should be deleted.");
    return new AccountDeletionRequest
    {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      UserName = user.DisplayName.Value,
      Reason = why.Length > 1000 ? why[..1000] : why,
      RequestedAt = now,
      Status = DeletionRequestStatus.Pending
    };
  }

  /// Approved by someone else; the caller deletes the account in the same save.
  public void Approve(Guid? by, string byName, string? remarks, DateTime now) => Decide(DeletionRequestStatus.Approved, by, byName, remarks, now);

  public void Reject(Guid? by, string byName, string? remarks, DateTime now)
  {
    if (string.IsNullOrWhiteSpace(remarks))
      throw new DomainException("Say why the account is not deleted, so the person knows.");
    Decide(DeletionRequestStatus.Rejected, by, byName, remarks, now);
  }

  /// The person takes it back before it is decided.
  public void Withdraw(DateTime now)
  {
    EnsurePending();
    Status = DeletionRequestStatus.Withdrawn;
    DecidedAt = now;
  }

  private void Decide(DeletionRequestStatus status, Guid? by, string byName, string? remarks, DateTime now)
  {
    EnsurePending();
    if (by is { } who && who == UserId.Value)
      throw new DomainException("Nobody decides the deletion of their own account; another administrator does.");
    Status = status;
    DecidedById = by;
    DecidedByName = byName;
    DecidedAt = now;
    var text = remarks?.Trim();
    Remarks = string.IsNullOrEmpty(text) ? null : text.Length > 1000 ? text[..1000] : text;
  }

  private void EnsurePending()
  {
    if (!IsPending)
      throw new DomainException($"This request was already {Status.ToString().ToLowerInvariant()}.");
  }
}
