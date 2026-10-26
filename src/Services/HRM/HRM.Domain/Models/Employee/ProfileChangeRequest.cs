/// A change an employee asks for to their own personal details (Employee Portal › My profile): a phone number, an
/// address, an emergency contact, a family member or the photo. Nothing on the record changes until HR approves it;
/// HR may reject it with the reason, and the employee may withdraw it while it waits. One change per request, so each
/// is decided on its own.
public class ProfileChangeRequest : Aggregate<ProfileChangeRequestId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public ProfileChangeArea Area { get; private set; }
  public ProfileChangeAction Action { get; private set; }
  /// The row changed or removed (a contact, address, emergency contact or family member); null when adding.
  public Guid? TargetId { get; private set; }
  /// The new values, as HR's own form would send them (jsonb); null for a removal or a photo.
  public string? Proposed { get; private set; }
  /// The change in words ("Add family member: Ali Khan, Son, born 3 Mar 2015").
  public string Summary { get; private set; } = default!;
  /// What the record said when the change was asked for (a change or a removal).
  public string? Before { get; private set; }
  /// The new photo, stored on the file server until HR decides.
  public string? PhotoPath { get; private set; }
  public string? Note { get; private set; }
  /// A paper that proves the change (a B-form, a nikah nama), one of the employee's own documents.
  public EmployeeDocumentId? SupportingDocumentId { get; private set; }
  public ProfileChangeStatus Status { get; private set; }
  public DateTime SubmittedAt { get; private set; }
  /// The Identity user who asked for it (the employee's own login).
  public Guid SubmittedBy { get; private set; }
  public Guid? ReviewedBy { get; private set; }
  public DateTime? ReviewedAt { get; private set; }
  public string? ReviewRemarks { get; private set; }

  public bool IsPending => Status == ProfileChangeStatus.Pending;

  public static ProfileChangeRequest Submit(
      ProfileChangeRequestId id,
      Employee employee,
      ProfileChangeArea area,
      ProfileChangeAction action,
      Guid? targetId,
      string? proposedJson,
      string summary,
      string? before,
      string? photoPath,
      string? note,
      EmployeeDocument? supportingDocument,
      Guid? submittedBy,
      DateTime submittedAt)
  {
    ArgumentNullException.ThrowIfNull(employee);
    employee.EnsureProfileActive();

    if (area == ProfileChangeArea.Photo)
    {
      if (action != ProfileChangeAction.Change)
        throw new DomainException("A photo can only be replaced.");
      if (string.IsNullOrWhiteSpace(photoPath))
        throw new DomainException("Choose the new photo.");
    }
    else
    {
      if (action == ProfileChangeAction.Add && targetId is not null)
        throw new DomainException("A new entry has nothing to change yet.");
      if (action != ProfileChangeAction.Add && (targetId is null || targetId == Guid.Empty))
        throw new DomainException("Say which entry is to be changed or removed.");
      if (action == ProfileChangeAction.Remove && proposedJson is not null)
        throw new DomainException("A removal carries no new values.");
      if (action != ProfileChangeAction.Remove && string.IsNullOrWhiteSpace(proposedJson))
        throw new DomainException("Give the new details.");
    }

    if (supportingDocument is not null && supportingDocument.EmployeeId != employee.Id)
      throw new DomainException("The supporting paper belongs to a different employee.");

    return new ProfileChangeRequest
    {
      Id = id,
      EmployeeId = employee.Id,
      Area = area,
      Action = action,
      TargetId = area == ProfileChangeArea.Photo ? null : targetId,
      Proposed = area == ProfileChangeArea.Photo ? null : proposedJson,
      Summary = Guard.RequiredText(summary, 500, "Summary"),
      Before = Guard.Text(before, 500, "Before"),
      PhotoPath = area == ProfileChangeArea.Photo ? photoPath : null,
      Note = Guard.Text(note, 1000, "Note"),
      SupportingDocumentId = supportingDocument?.Id,
      Status = ProfileChangeStatus.Pending,
      SubmittedBy = Guard.Actor(submittedBy, "ask for a change"),
      SubmittedAt = submittedAt
    };
  }

  /// HR approves: the caller then makes the change on the record, in the same save.
  public void Approve(Guid? reviewedBy, DateTime at, string? remarks) => Decide(ProfileChangeStatus.Approved, reviewedBy, at, remarks);

  public void Reject(Guid? reviewedBy, DateTime at, string? reason)
  {
    if (string.IsNullOrWhiteSpace(reason))
      throw new DomainException("Say why the change is not accepted, so the employee can put it right.");
    Decide(ProfileChangeStatus.Rejected, reviewedBy, at, reason);
  }

  /// The employee takes back a change HR has not decided yet.
  public void Withdraw(DateTime at)
  {
    EnsurePending();
    Status = ProfileChangeStatus.Withdrawn;
    ReviewedAt = at;
  }

  private void Decide(ProfileChangeStatus status, Guid? reviewedBy, DateTime at, string? remarks)
  {
    EnsurePending();
    ReviewedBy = Guard.Actor(reviewedBy, "decide a profile change");
    ReviewedAt = at;
    ReviewRemarks = Guard.Text(remarks, 1000, "Remarks");
    Status = status;
  }

  private void EnsurePending()
  {
    if (!IsPending)
      throw new DomainException($"This change was already {EnumText.Words(Status)}.");
  }
}
