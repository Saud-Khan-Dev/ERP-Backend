/// The content of an HR action: what changes, for whom, from when, and what applying it needs (the parent department
/// of a deputation, the employment method of a regularization, the pay to start on, the event type of a custom event,
/// the last day of leave without pay ...). Everything is captured in the draft, so the approver approves the whole act.
public sealed record HrActionDetails(
  HrActionType ActionType,
  PostId? NewPostId,
  PayScaleGradeId? NewGradeId,
  OrganizationUnitId? NewOrgUnitId,
  DateOnly EffectiveDate,
  string? OrderNumber,
  string? Reason,
  EmployeeDocumentId? SupportingDocumentId,
  DateOnly? EndDate = null,
  ServiceEventTypeId? EventTypeId = null,
  string? ExternalReferenceOrg = null,
  EmploymentMethod? EmploymentMethod = null,
  RecruitmentMethodId? RecruitmentMethodId = null,
  int? StageNumber = null,
  decimal? BasicPay = null,
  int? NoticePeriodDays = null);

/// A proposed HR change (appointment, transfer, promotion, suspension ...): draft -> pending -> approved -> applied,
/// or rejected / cancelled on the way. The "old" side is captured from the employee's current post when the action is
/// drafted, and checked again when it is applied. The rules (HrActionRules) run at draft, submit, approve and apply.
/// Applying writes the service-history row (and the assignment, pay, leave and status changes) and links it here.
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
  /// The last day of leave without pay, or the end of a deputation out.
  public DateOnly? EndDate { get; private set; }
  public string? OrderNumber { get; private set; }
  public string? Reason { get; private set; }
  public EmployeeDocumentId? SupportingDocumentId { get; private set; }
  /// The service event type of a custom event (ActionType Other).
  public ServiceEventTypeId? EventTypeId { get; private set; }
  /// The other department of a deputation in / out.
  public string? ExternalReferenceOrg { get; private set; }
  public EmploymentMethod? EmploymentMethod { get; private set; }
  public RecruitmentMethodId? RecruitmentMethodId { get; private set; }
  /// The pay to start on (appointment): a stage of the post's scale, or a basic pay different from the stage's.
  public int? StageNumber { get; private set; }
  public decimal? BasicPay { get; private set; }
  public int? NoticePeriodDays { get; private set; }
  public HrActionStatus Status { get; private set; }
  /// Pointer into a platform-wide approval workflow, when one exists (no FK).
  public Guid? ApprovalRequestId { get; private set; }
  public Guid? SubmittedBy { get; private set; }
  public DateTime? SubmittedAt { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateTime? ApprovedAt { get; private set; }
  /// Who returned, rejected or cancelled it last, when and why.
  public Guid? DecidedBy { get; private set; }
  public DateTime? DecidedAt { get; private set; }
  public string? DecisionRemarks { get; private set; }
  public Guid? AppliedBy { get; private set; }
  public DateTime? AppliedAt { get; private set; }
  public ServiceHistoryId? ResultingServiceHistoryId { get; private set; }
  /// The approved leave of the leave-without-pay type an LWOP action created (or took over).
  public LeaveApplicationId? LeaveApplicationId { get; private set; }

  public HrActionDetails Details => new(ActionType, NewPostId, NewGradeId, NewOrgUnitId, EffectiveDate, OrderNumber, Reason,
    SupportingDocumentId, EndDate, EventTypeId, ExternalReferenceOrg, EmploymentMethod, RecruitmentMethodId, StageNumber, BasicPay, NoticePeriodDays);

  public bool IsOpen => Status is HrActionStatus.Draft or HrActionStatus.Pending or HrActionStatus.Approved;

  /// Actions that move the employee to another post.
  public static bool NeedsNewPost(HrActionType type) => type is HrActionType.Appointment or HrActionType.Transfer
    or HrActionType.Promotion or HrActionType.Demotion or HrActionType.DeputationIn;

  /// Actions that end the employee's service.
  public static bool EndsService(HrActionType type) => SeparationFor(type) is not null;

  /// The separation an action records; null when the action does not end service.
  public static SeparationType? SeparationFor(HrActionType type) => type switch
  {
    HrActionType.Retirement => SeparationType.Retirement,
    HrActionType.Resignation => SeparationType.Resignation,
    HrActionType.Termination => SeparationType.Termination,
    HrActionType.Death => SeparationType.Death,
    HrActionType.Dismissal => SeparationType.Dismissal,
    HrActionType.RemovalFromService => SeparationType.RemovalFromService,
    HrActionType.CompulsoryRetirement => SeparationType.CompulsoryRetirement,
    HrActionType.PrematureRetirement => SeparationType.PrematureRetirement,
    HrActionType.EndOfContract => SeparationType.EndOfContract,
    HrActionType.DeputationEnd => SeparationType.DeputationEnd,
    _ => null
  };

  /// Actions that change an undated fact (the service status or the employment type): they are applied on or after
  /// their date, never before. Posts, pay and leave are dated, so those actions may be applied ahead.
  public static bool TakesEffectOnItsDate(HrActionType type) => EndsService(type) || type is HrActionType.Joining
    or HrActionType.DeputationIn or HrActionType.DeputationOut or HrActionType.Regularization or HrActionType.Suspension
    or HrActionType.Reinstatement;

  /// `current` = the employee's regular post on the effective date (post, grade, unit), or null when there is none.
  /// The caller has checked the rules (HrActionRules.Check) with the facts of that date.
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

  public void Submit(Guid? submittedBy, DateTime at)
  {
    Move(HrActionStatus.Draft, HrActionStatus.Pending, "submitted");
    SubmittedBy = Guard.Actor(submittedBy, "submit an HR action");
    SubmittedAt = at;
  }

  /// Sent back to the drafter for changes, with the reason.
  public void ReturnToDraft(Guid? returnedBy, DateTime at, string? remarks)
  {
    Move(HrActionStatus.Pending, HrActionStatus.Draft, "returned");
    Decide(returnedBy, at, remarks, "return an HR action");
  }

  /// `requireSeparateApprover`: the person who drafted or submitted the action may not approve it (maker-checker).
  public void Approve(Guid? approvedBy, DateTime approvedAt, Guid? approvalRequestId, bool requireSeparateApprover)
  {
    EnsureStatus(HrActionStatus.Pending, "approved");
    var approver = Guard.Actor(approvedBy, "approve an HR action");
    if (requireSeparateApprover && (approver == CreatedBy || approver == SubmittedBy))
      throw new DomainException("You drafted or submitted this HR action, so another officer must approve it.");

    Status = HrActionStatus.Approved;
    ApprovedBy = approver;
    ApprovedAt = approvedAt;
    ApprovalRequestId = approvalRequestId ?? ApprovalRequestId;
  }

  public void Reject(Guid? rejectedBy, DateTime at, string? remarks)
  {
    if (string.IsNullOrWhiteSpace(remarks))
      throw new DomainException("Give the reason for rejecting the HR action.");
    Move(HrActionStatus.Pending, HrActionStatus.Rejected, "rejected");
    Decide(rejectedBy, at, remarks, "reject an HR action");
  }

  public void Cancel(Guid? cancelledBy, DateTime at, string? remarks)
  {
    if (!IsOpen)
      throw new DomainException($"An HR action that is {Describe(Status)} cannot be cancelled.");

    Status = HrActionStatus.Cancelled;
    Decide(cancelledBy, at, remarks, "cancel an HR action");
  }

  /// Closed by the system (the employee left service before it was applied).
  public void Close(string remarks, DateTime at)
  {
    if (!IsOpen)
      return;

    Status = HrActionStatus.Cancelled;
    DecidedAt = at;
    DecisionRemarks = Guard.Text(remarks, 1000, "Remarks");
  }

  /// Checks the action still fits the employee's current post before it is applied, and that its date has come for an
  /// action that changes an undated fact.
  public void EnsureStillCurrent(ServiceChange current, DateOnly today)
  {
    EnsureStatus(HrActionStatus.Approved, "applied");

    if (current.OldPostId != OldPostId)
      throw new DomainException("The employee's post has changed since this action was drafted. Cancel it and draft a new one.");

    if (TakesEffectOnItsDate(ActionType) && EffectiveDate > today)
      throw new DomainException($"This {EnumText.Words(ActionType)} takes effect on {EffectiveDate:dd MMM yyyy}; apply it on or after that day.");
  }

  public void MarkApplied(ServiceHistoryId serviceHistoryId, Guid? appliedBy, DateTime at)
  {
    ArgumentNullException.ThrowIfNull(serviceHistoryId);
    EnsureStatus(HrActionStatus.Approved, "applied");
    Status = HrActionStatus.Applied;
    ResultingServiceHistoryId = serviceHistoryId;
    AppliedBy = appliedBy;
    AppliedAt = at;
  }

  public void LinkLeave(LeaveApplicationId leaveApplicationId) => LeaveApplicationId = leaveApplicationId;

  private void Decide(Guid? by, DateTime at, string? remarks, string act)
  {
    DecidedBy = Guard.Actor(by, act);
    DecidedAt = at;
    DecisionRemarks = Guard.Text(remarks, 1000, "Remarks");
  }

  private void Apply(HrActionDetails details, ServiceChange current)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(current);

    if (NeedsNewPost(details.ActionType) && details.NewPostId is null)
      throw new DomainException($"{Capitalize(EnumText.WithArticle(details.ActionType))} needs the new post.");

    if (details.NewPostId is not null && details.NewPostId == current.OldPostId)
      throw new DomainException("The new post is the employee's current post.");

    Guard.DateOrder(details.EffectiveDate, details.EndDate, "The start", "The end");

    ActionType = details.ActionType;
    OldPostId = current.OldPostId;
    OldGradeId = current.OldGradeId;
    OldOrgUnitId = current.OldOrgUnitId;
    NewPostId = details.NewPostId;
    NewGradeId = details.NewGradeId;
    NewOrgUnitId = details.NewOrgUnitId;
    EffectiveDate = details.EffectiveDate;
    EndDate = details.EndDate;
    OrderNumber = Guard.Text(details.OrderNumber, 100, "Order number");
    Reason = Guard.Text(details.Reason, 4000, "Reason");
    SupportingDocumentId = details.SupportingDocumentId;
    EventTypeId = details.EventTypeId;
    ExternalReferenceOrg = Guard.Text(details.ExternalReferenceOrg, 200, "Other department");
    EmploymentMethod = details.EmploymentMethod;
    RecruitmentMethodId = details.RecruitmentMethodId;
    StageNumber = Guard.Between(details.StageNumber, 0, 100, "Stage");
    BasicPay = Guard.Money(details.BasicPay, "Basic pay");
    NoticePeriodDays = details.NoticePeriodDays is < 0 ? throw new DomainException("Notice period cannot be negative.") : details.NoticePeriodDays;
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
    if (SettlementPayrollTransactionId is not null)
      throw new DomainException("The final settlement has been paid through payroll; its figures can no longer change.");

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

  /// The settlement run was reversed: the separation waits for its replacement.
  public void UnlinkSettlement() => SettlementPayrollTransactionId = null;

  public void LinkServiceHistory(ServiceHistoryId serviceHistoryId) => ServiceHistoryId = serviceHistoryId;
}
