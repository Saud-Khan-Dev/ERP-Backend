/// A periodic access review: an administrator goes through every active account - its roles, exceptions, where it
/// works, when it last signed in - and keeps, changes or switches off each one. The review is signed off when every
/// account is decided; the next one falls due SecuritySettings.AccessReviewMonths later.
public class AccessReview : Entity<Guid>
{
  public DateTime StartedAt { get; private set; }
  public Guid? StartedById { get; private set; }
  public string StartedByName { get; private set; } = default!;
  /// The accounts to review when it started.
  public int AccountCount { get; private set; }
  public DateTime? ClosedAt { get; private set; }
  public string? ClosedByName { get; private set; }
  public string? Note { get; private set; }

  public bool IsOpen => ClosedAt is null;

  public static AccessReview Start(Guid? startedById, string startedByName, int accountCount, DateTime now) => new()
  {
    Id = Guid.NewGuid(),
    StartedAt = now,
    StartedById = startedById,
    StartedByName = startedByName,
    AccountCount = accountCount
  };

  public void Close(string closedByName, string? note, DateTime now)
  {
    if (!IsOpen)
      throw new DomainException("This review is already signed off.");
    ClosedAt = now;
    ClosedByName = closedByName;
    Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)];
  }
}

/// One account's decision in a review, with what it held at the time (so the review reads the same later).
public class AccessReviewDecision : Entity<Guid>
{
  public Guid ReviewId { get; private set; }
  public UserId UserId { get; private set; } = default!;
  public string UserName { get; private set; } = default!;
  public ReviewOutcome Outcome { get; private set; }
  /// Roles, exceptions and scope in words when decided ("Tax clerk · Nathia Gali").
  public string AccessText { get; private set; } = default!;
  public DateTime? LastSignInAt { get; private set; }
  public string? Note { get; private set; }
  public DateTime DecidedAt { get; private set; }
  public Guid? DecidedById { get; private set; }
  public string DecidedByName { get; private set; } = default!;

  public static AccessReviewDecision Decide(Guid reviewId, UserId userId, string userName, ReviewOutcome outcome, string accessText,
      DateTime? lastSignInAt, string? note, Guid? decidedById, string decidedByName, DateTime now)
  {
    if (outcome != ReviewOutcome.Kept && string.IsNullOrWhiteSpace(note))
      throw new DomainException("Say what was changed and why.");
    return new AccessReviewDecision
    {
      Id = Guid.NewGuid(),
      ReviewId = reviewId,
      UserId = userId,
      UserName = userName,
      Outcome = outcome,
      AccessText = accessText.Length > 1000 ? accessText[..1000] : accessText,
      LastSignInAt = lastSignInAt,
      Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 500)],
      DecidedAt = now,
      DecidedById = decidedById,
      DecidedByName = decidedByName
    };
  }
}

public enum ReviewOutcome
{
  /// The access is right as it is.
  Kept,
  /// Roles, exceptions or scope were changed during the review.
  Changed,
  /// The account was switched off.
  SwitchedOff
}
