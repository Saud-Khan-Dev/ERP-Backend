/// Kinds of service event (Appointment, Transfer, Promotion ...). category groups them: entry, movement, status,
/// disciplinary, exit.
public class ServiceEventType : Aggregate<ServiceEventTypeId>
{
  public string Name { get; private set; } = default!;
  public string? Category { get; private set; }
  public bool IsActive { get; private set; }

  public static ServiceEventType Create(ServiceEventTypeId id, string name, string? category)
  {
    var type = new ServiceEventType { Id = id, IsActive = true };
    type.Update(name, category);
    return type;
  }

  public void Update(string name, string? category)
  {
    Name = Guard.RequiredText(name, 100, "Event type name");
    Category = Guard.Text(category, 50, "Category")?.ToLowerInvariant();
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Service event type '{Name}' is inactive.");
  }
}

/// The event-type names HR actions and separations record under. The seeder keeps them present; they cannot be renamed
/// or deactivated while the code depends on them.
public static class ServiceEventNames
{
  public const string Appointment = "Appointment";
  public const string Joining = "Joining";
  public const string Transfer = "Transfer";
  public const string Promotion = "Promotion";
  public const string Demotion = "Demotion";
  public const string DeputationIn = "Deputation In";
  public const string DeputationOut = "Deputation Out";
  public const string Regularization = "Regularization";
  public const string Lwop = "LWOP";
  public const string Suspension = "Suspension";
  public const string Reinstatement = "Reinstatement";
  public const string Retirement = "Retirement";
  public const string Resignation = "Resignation";
  public const string Termination = "Termination";
  public const string Death = "Death";
  public const string Dismissal = "Dismissal";
  public const string EndOfContract = "End of Contract";
  public const string DeputationEnd = "Deputation End";

  public static readonly IReadOnlyList<(string Name, string Category)> All =
  [
    (Appointment, "entry"), (Joining, "entry"), (Transfer, "movement"), (Promotion, "movement"), (Demotion, "movement"),
    (DeputationIn, "movement"), (DeputationOut, "movement"), (Regularization, "status"), (Lwop, "status"),
    (Suspension, "disciplinary"), (Reinstatement, "disciplinary"), (Retirement, "exit"), (Resignation, "exit"),
    (Termination, "exit"), (Death, "exit"), (Dismissal, "exit"), (EndOfContract, "exit"), (DeputationEnd, "exit")
  ];

  public static bool IsSystem(string name) => All.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

  /// The event an HR action is recorded as; null for "other" (the caller names the event type).
  public static string? For(HrActionType type) => type switch
  {
    HrActionType.Appointment => Appointment,
    HrActionType.Joining => Joining,
    HrActionType.Transfer => Transfer,
    HrActionType.Promotion => Promotion,
    HrActionType.Demotion => Demotion,
    HrActionType.DeputationIn => DeputationIn,
    HrActionType.DeputationOut => DeputationOut,
    HrActionType.Regularization => Regularization,
    HrActionType.Lwop => Lwop,
    HrActionType.Suspension => Suspension,
    HrActionType.Reinstatement => Reinstatement,
    HrActionType.Retirement => Retirement,
    HrActionType.Resignation => Resignation,
    HrActionType.Termination => Termination,
    HrActionType.Death => Death,
    _ => null
  };

  /// The event a separation is recorded as; null for "other".
  public static string? For(SeparationType type) => type switch
  {
    SeparationType.Retirement => Retirement,
    SeparationType.Resignation => Resignation,
    SeparationType.Termination => Termination,
    SeparationType.Dismissal => Dismissal,
    SeparationType.Death => Death,
    SeparationType.EndOfContract => EndOfContract,
    SeparationType.DeputationEnd => DeputationEnd,
    _ => null
  };
}

/// Old and new post / grade / org unit around a service event (all optional).
public sealed record ServiceChange(
  PostId? OldPostId,
  PostId? NewPostId,
  PayScaleGradeId? OldGradeId,
  PayScaleGradeId? NewGradeId,
  OrganizationUnitId? OldOrgUnitId,
  OrganizationUnitId? NewOrgUnitId)
{
  public static readonly ServiceChange None = new(null, null, null, null, null, null);
}

/// Append-only service ledger: one row per service event (appointment, transfer, promotion, deputation ...), what
/// happened and why. The resulting occupancy of a post is in position_assignment. Rows are never updated or deleted
/// (a database trigger refuses it); a mistake is corrected by recording another event.
public class EmployeeServiceHistory : Aggregate<ServiceHistoryId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public ServiceEventTypeId EventTypeId { get; private set; } = default!;
  public DateOnly EffectiveDate { get; private set; }
  public PostId? OldPostId { get; private set; }
  public PostId? NewPostId { get; private set; }
  public PayScaleGradeId? OldGradeId { get; private set; }
  public PayScaleGradeId? NewGradeId { get; private set; }
  public OrganizationUnitId? OldOrgUnitId { get; private set; }
  public OrganizationUnitId? NewOrgUnitId { get; private set; }
  public RecruitmentMethodId? RecruitmentMethodId { get; private set; }
  /// The other department of a deputation in / out.
  public string? ExternalReferenceOrg { get; private set; }
  public string? OrderNumber { get; private set; }
  public EmployeeDocumentId? SupportingDocumentId { get; private set; }
  public string? Reason { get; private set; }
  public string? Remarks { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  public ServiceChange Change => new(OldPostId, NewPostId, OldGradeId, NewGradeId, OldOrgUnitId, NewOrgUnitId);

  public static EmployeeServiceHistory Record(
      ServiceHistoryId id,
      EmployeeId employeeId,
      ServiceEventType eventType,
      DateOnly effectiveDate,
      ServiceChange change,
      RecruitmentMethodId? recruitmentMethodId,
      string? externalReferenceOrg,
      string? orderNumber,
      EmployeeDocumentId? supportingDocumentId,
      string? reason,
      string? remarks,
      Guid? approvedBy)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(eventType);
    ArgumentNullException.ThrowIfNull(change);
    eventType.EnsureActive();

    return new EmployeeServiceHistory
    {
      Id = id,
      EmployeeId = employeeId,
      EventTypeId = eventType.Id,
      EffectiveDate = effectiveDate,
      OldPostId = change.OldPostId,
      NewPostId = change.NewPostId,
      OldGradeId = change.OldGradeId,
      NewGradeId = change.NewGradeId,
      OldOrgUnitId = change.OldOrgUnitId,
      NewOrgUnitId = change.NewOrgUnitId,
      RecruitmentMethodId = recruitmentMethodId,
      ExternalReferenceOrg = Guard.Text(externalReferenceOrg, 200, "Other department"),
      OrderNumber = Guard.Text(orderNumber, 100, "Order number"),
      SupportingDocumentId = supportingDocumentId,
      Reason = Guard.Text(reason, 4000, "Reason"),
      Remarks = Guard.Text(remarks, 4000, "Remarks"),
      ApprovedBy = approvedBy
    };
  }
}
