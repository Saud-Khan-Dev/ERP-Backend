/// The content of an HR action: what changes, for whom, from when.
public sealed record HrActionDetails(
  HrActionType ActionType,
  PostId? NewPostId,
  PayScaleGradeId? NewGradeId,
  OrganizationUnitId? NewOrgUnitId,
  DateOnly EffectiveDate,
  string? OrderNumber,
  string? Reason,
  EmployeeDocumentId? SupportingDocumentId);

/// A proposed HR change (appointment, transfer, promotion, suspension ...): draft -> pending -> approved -> applied,
/// or rejected / cancelled on the way. The "old" side is captured from the employee's current post when the action is
/// drafted, and checked again when it is applied. Applying writes the service-history row (and the assignment, pay
/// and status changes) and links it here.
public class HrActionRequest : Aggregate<HrActionRequestId>
{
  public HrActionType ActionType { get; private set; }
  public EmployeeId EmployeeId { get; private set; } = default!;
  public PostId? OldPostId { get; private set; }
  public PostId? NewPostId { get; private set; }
  public PayScaleGradeId? OldGradeId { get; private set; }
  public PayScaleGradeId? NewGradeId { get; private set; }
  public OrganizationUnitId? OldOrgUnitId { get; private set; }
  public OrganizationUnitId? NewOrgUnitId { get; private set; }
  public DateOnly EffectiveDate { get; private set; }
  public string? OrderNumber { get; private set; }
  public string? Reason { get; private set; }
  public EmployeeDocumentId? SupportingDocumentId { get; private set; }
  public HrActionStatus Status { get; private set; }
  /// Pointer into a platform-wide approval workflow, when one exists (no FK).
  public Guid? ApprovalRequestId { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }
  public ServiceHistoryId? ResultingServiceHistoryId { get; private set; }

  /// Actions that move the employee to another post.
  public static bool NeedsNewPost(HrActionType type) => type is HrActionType.Appointment or HrActionType.Transfer
    or HrActionType.Promotion or HrActionType.Demotion or HrActionType.DeputationIn;

  /// Actions that end the employee's service.
  public static bool EndsService(HrActionType type) => type is HrActionType.Retirement or HrActionType.Resignation
    or HrActionType.Termination or HrActionType.Death;

  /// `current` = the employee's regular post on the effective date (post, grade, unit), or null when there is none.
  public static HrActionRequest Draft(HrActionRequestId id, Employee employee, HrActionDetails details, ServiceChange current)
  {
    ArgumentNullException.ThrowIfNull(employee);
    employee.EnsureProfileActive();

    var action = new HrActionRequest { Id = id, EmployeeId = employee.Id, Status = HrActionStatus.Draft };
    action.Apply(details, current);
    return action;
  }

  public void UpdateDraft(HrActionDetails details, ServiceChange current)
  {
    EnsureStatus(HrActionStatus.Draft, "changed");
    Apply(details, current);
  }

  public void Submit() => Move(HrActionStatus.Draft, HrActionStatus.Pending, "submitted");

  /// Sent back to the drafter for changes.
  public void ReturnToDraft() => Move(HrActionStatus.Pending, HrActionStatus.Draft, "returned");

  public void Approve(Guid? approvedBy, DateTime approvedAt, Guid? approvalRequestId)
  {
    Move(HrActionStatus.Pending, HrActionStatus.Approved, "approved");
    ApprovedBy = Guard.Actor(approvedBy, "approve an HR action");
    ApprovedAt = approvedAt;
    ApprovalRequestId = approvalRequestId ?? ApprovalRequestId;
  }

  public void Reject() => Move(HrActionStatus.Pending, HrActionStatus.Rejected, "rejected");

  public void Cancel()
  {
    if (Status is not (HrActionStatus.Draft or HrActionStatus.Pending or HrActionStatus.Approved))
      throw new DomainException($"An HR action that is {Describe(Status)} cannot be cancelled.");

    Status = HrActionStatus.Cancelled;
  }

  /// Checks the action still fits the employee's current post before it is applied.
  public void EnsureStillCurrent(ServiceChange current)
  {
    EnsureStatus(HrActionStatus.Approved, "applied");

    if (current.OldPostId != OldPostId)
      throw new DomainException("The employee's post has changed since this action was drafted. Cancel it and draft a new one.");
  }

  public void MarkApplied(ServiceHistoryId serviceHistoryId)
  {
    ArgumentNullException.ThrowIfNull(serviceHistoryId);
    EnsureStatus(HrActionStatus.Approved, "applied");
    Status = HrActionStatus.Applied;
    ResultingServiceHistoryId = serviceHistoryId;
  }

  private void Apply(HrActionDetails details, ServiceChange current)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(current);

    if (NeedsNewPost(details.ActionType) && details.NewPostId is null)
      throw new DomainException($"{Capitalize(EnumText.WithArticle(details.ActionType))} needs the new post.");

    if (details.ActionType == HrActionType.Appointment && current.OldPostId is not null)
      throw new DomainException("The employee already holds a regular post; use a transfer or promotion instead.");

    if ((details.ActionType is HrActionType.Transfer or HrActionType.Promotion or HrActionType.Demotion) && current.OldPostId is null)
      throw new DomainException($"{Capitalize(EnumText.WithArticle(details.ActionType))} moves the employee from a post, but the employee holds no regular post on {details.EffectiveDate:yyyy-MM-dd}.");

    if (details.NewPostId is not null && details.NewPostId == current.OldPostId)
      throw new DomainException("The new post is the employee's current post.");

    ActionType = details.ActionType;
    OldPostId = current.OldPostId;
    OldGradeId = current.OldGradeId;
    OldOrgUnitId = current.OldOrgUnitId;
    NewPostId = details.NewPostId;
    NewGradeId = details.NewGradeId;
    NewOrgUnitId = details.NewOrgUnitId;
    EffectiveDate = details.EffectiveDate;
    OrderNumber = Guard.Text(details.OrderNumber, 100, "Order number");
    Reason = Guard.Text(details.Reason, 4000, "Reason");
    SupportingDocumentId = details.SupportingDocumentId;
  }

  private void Move(HrActionStatus from, HrActionStatus to, string verb)
  {
    EnsureStatus(from, verb);
    Status = to;
  }

  private void EnsureStatus(HrActionStatus expected, string verb)
  {
    if (Status != expected)
      throw new DomainException($"Only {EnumText.WithArticle(expected)} HR action can be {verb}; this one is {Describe(Status)}.");
  }

  private static string Describe(Enum value) => EnumText.Words(value);

  private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}

/// An employee leaving service: retirement, resignation, termination, dismissal, death, end of contract, end of
/// deputation. One row per employee; the settlement figures are filled in as they are worked out.
public class EmployeeSeparation : Aggregate<SeparationId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public SeparationType SeparationType { get; private set; }
  public DateOnly SeparationDate { get; private set; }
  public ServiceHistoryId? ServiceHistoryId { get; private set; }
  public PayrollTransactionId? SettlementPayrollTransactionId { get; private set; }
  public string? OrderNumber { get; private set; }
  public string? Reason { get; private set; }
  public int? NoticePeriodDays { get; private set; }
  public decimal? FinalSettlementAmount { get; private set; }
  public decimal? OutstandingLoanAmount { get; private set; }
  public decimal? LeaveEncashmentAmount { get; private set; }
  public string? PensionReference { get; private set; }

  public static EmployeeSeparation Record(
      SeparationId id,
      EmployeeId employeeId,
      SeparationType type,
      DateOnly separationDate,
      ServiceHistoryId? serviceHistoryId,
      string? orderNumber,
      string? reason,
      int? noticePeriodDays,
      decimal? outstandingLoanAmount)
  {
    ArgumentNullException.ThrowIfNull(employeeId);

    return new EmployeeSeparation
    {
      Id = id,
      EmployeeId = employeeId,
      SeparationType = type,
      SeparationDate = separationDate,
      ServiceHistoryId = serviceHistoryId,
      OrderNumber = Guard.Text(orderNumber, 100, "Order number"),
      Reason = Guard.Text(reason, 4000, "Reason"),
      NoticePeriodDays = noticePeriodDays is < 0 ? throw new DomainException("Notice period cannot be negative.") : noticePeriodDays,
      OutstandingLoanAmount = Guard.Money(outstandingLoanAmount, "Outstanding loans")
    };
  }

  public void UpdateSettlement(
      string? orderNumber,
      string? reason,
      int? noticePeriodDays,
      decimal? finalSettlementAmount,
      decimal? outstandingLoanAmount,
      decimal? leaveEncashmentAmount,
      string? pensionReference)
  {
    OrderNumber = Guard.Text(orderNumber, 100, "Order number");
    Reason = Guard.Text(reason, 4000, "Reason");
    NoticePeriodDays = noticePeriodDays is < 0 ? throw new DomainException("Notice period cannot be negative.") : noticePeriodDays;
    // the final settlement may be negative (the employee owes GDA); the parts may not
    FinalSettlementAmount = finalSettlementAmount.HasValue ? decimal.Round(finalSettlementAmount.Value, 2, MidpointRounding.AwayFromZero) : null;
    OutstandingLoanAmount = Guard.Money(outstandingLoanAmount, "Outstanding loans");
    LeaveEncashmentAmount = Guard.Money(leaveEncashmentAmount, "Leave encashment");
    PensionReference = Guard.Text(pensionReference, 100, "Pension reference");
  }

  public void LinkSettlement(PayrollTransactionId payrollTransactionId) => SettlementPayrollTransactionId = payrollTransactionId;

  public void LinkServiceHistory(ServiceHistoryId serviceHistoryId) => ServiceHistoryId = serviceHistoryId;
}
