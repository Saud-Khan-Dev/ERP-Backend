/// One submitted building / site plan or revision of it. The structured facts live here; the drawing
/// itself is a property_document with entity_type = BUILDING_PLAN (req. §29).
///
/// A revision is a new row with the same plan_no, revision_no + 1 and supersedes_plan_id pointing at the
/// previous one, which becomes REVISED.
public class BuildingPlan : Aggregate<BuildingPlanId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  /// BP-00001, issued by the BUILDING_PLAN code sequence and kept across revisions.
  public BusinessCode PlanNo { get; private set; } = default!;
  public MasterId BuildingPlanTypeId { get; private set; } = default!;
  public MasterId BuildingPlanStatusId { get; private set; } = default!;
  public int RevisionNo { get; private set; }
  public BuildingPlanId? SupersedesPlanId { get; private set; }
  public OwnerId? ApplicantOwnerId { get; private set; }
  public DateOnly? SubmissionDate { get; private set; }
  public DateOnly? ApprovalDate { get; private set; }
  /// The approving officer: the name as it stood then, and their HR record when picked from the staff list.
  public string? ApprovedBy { get; private set; }
  public Guid? ApprovedByEmployeeId { get; private set; }
  public string? ApprovalReferenceNo { get; private set; }
  /// The Taxation no-dues certificate (clearance) the approval relied on, when one was given.
  public string? NoDuesCertificateNo { get; private set; }
  public DateOnly? ValidityEndDate { get; private set; }
  public decimal? CoveredArea { get; private set; }
  public MasterId? MeasurementUnitId { get; private set; }
  /// The covered area in square feet (rule 7), converted with the unit's factor when it was entered.
  public decimal? CoveredAreaBase { get; private set; }
  public int? Floors { get; private set; }
  public string? ArchitectName { get; private set; }
  public string? Remarks { get; private set; }

  public sealed record Details(
      BuildingPlanType PlanType,
      PropertyOwner? Applicant,
      DateOnly? SubmissionDate,
      decimal? CoveredArea,
      MeasurementUnit? Unit,
      int? Floors,
      string? ArchitectName,
      string? Remarks);

  public static BuildingPlan Submit(BuildingPlanId id, Property property, BusinessCode planNo, BuildingPlanStatus submittedStatus, Details details)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(planNo);
    ArgumentNullException.ThrowIfNull(submittedStatus);
    property.EnsureActive();
    submittedStatus.EnsureIs(SystemMasterCodes.Submitted);

    var plan = new BuildingPlan { Id = id, PropertyId = property.Id, PlanNo = planNo, BuildingPlanStatusId = submittedStatus.Id, RevisionNo = 0 };
    plan.Apply(details);
    return plan;
  }

  public void Update(BuildingPlanStatus currentStatus, Details details)
  {
    EnsureUnderConsideration(currentStatus);
    Apply(details);
  }

  /// Under Review, Withdrawn ... Approval, rejection and revision have their own steps.
  public void ChangeStatus(BuildingPlanStatus currentStatus, BuildingPlanStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureUnderConsideration(currentStatus);
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Approved) || status.Is(SystemMasterCodes.Rejected) || status.Is(SystemMasterCodes.Revised))
      throw new DomainException("Use approve / reject / revise for these statuses.");

    BuildingPlanStatusId = status.Id;
  }

  /// What an approval records: the date, the approving officer (name, and HR record when picked), the order, how long
  /// it is valid and the no-dues certificate relied on.
  public sealed record Approval(
      DateOnly ApprovalDate,
      string ApprovedBy,
      Guid? ApprovedByEmployeeId,
      string? ApprovalReferenceNo,
      DateOnly? ValidityEndDate,
      string? NoDuesCertificateNo = null);

  public const int NoDuesCertificateMaxLength = 50;

  /// Approves the plan (a recorded decision: not in the future). slopeDegrees: the current boundary survey's slope -
  /// above the Building & Construction Regulations 2026 limit the approval goes through with a warning, never refused.
  public IReadOnlyList<string> Approve(BuildingPlanStatus currentStatus, BuildingPlanStatus approvedStatus, Approval approval, DateOnly? today = null, decimal? slopeDegrees = null)
  {
    ArgumentNullException.ThrowIfNull(approval);
    EnsureUnderConsideration(currentStatus);
    approvedStatus.EnsureIs(SystemMasterCodes.Approved);
    Guard.DateOrder(SubmissionDate, approval.ApprovalDate, "Submission date", "Approval date");
    Guard.DateOrder(approval.ApprovalDate, approval.ValidityEndDate, "Approval date", "Validity end date");

    if (today is { } day && approval.ApprovalDate > day)
      throw new DomainException($"The approval date cannot be in the future ({approval.ApprovalDate:yyyy-MM-dd}); today is {day:yyyy-MM-dd}.");

    BuildingPlanStatusId = approvedStatus.Id;
    ApprovalDate = approval.ApprovalDate;
    ApprovedBy = Guard.RequiredText(approval.ApprovedBy, 150, "Approved by");
    ApprovedByEmployeeId = approval.ApprovedByEmployeeId;
    ApprovalReferenceNo = Guard.Text(approval.ApprovalReferenceNo, 100, "Approval reference no.");
    ValidityEndDate = approval.ValidityEndDate;
    NoDuesCertificateNo = Guard.Text(approval.NoDuesCertificateNo, NoDuesCertificateMaxLength, "No-dues certificate no.");

    return slopeDegrees is { } slope && slope > PropertyBoundary.SteepSlopeDegrees
      ? new[] { $"The plot's surveyed slope is {slope:0.##}°, above the 45° limit of the Building & Construction Regulations 2026; the plan was approved as entered." }
      : Array.Empty<string>();
  }

  /// An appeal (s.32) set aside the rejection: the plan is under consideration again (SUBMITTED), with a remark.
  public void ReopenOnAppeal(BuildingPlanStatus currentStatus, BuildingPlanStatus submittedStatus, string appealNo)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    submittedStatus.EnsureIs(SystemMasterCodes.Submitted);

    if (currentStatus.Id != BuildingPlanStatusId)
      throw new DomainException("The supplied status does not match the plan.");

    if (!currentStatus.Is(SystemMasterCodes.Rejected))
      throw new DomainException($"Plan {PlanNo.Value} rev. {RevisionNo} is {currentStatus.Name.Value.ToLowerInvariant()}, not rejected, so the appeal has nothing to reopen.");

    BuildingPlanStatusId = submittedStatus.Id;
    Remarks = RemarkText.Append(Remarks, $"Rejection set aside on appeal {appealNo}; under consideration again.");
  }

  public void Reject(BuildingPlanStatus currentStatus, BuildingPlanStatus rejectedStatus, string? remarks)
  {
    EnsureUnderConsideration(currentStatus);
    rejectedStatus.EnsureIs(SystemMasterCodes.Rejected);
    BuildingPlanStatusId = rejectedStatus.Id;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// A revised plan (after rejection, or a revision of an approved plan): same plan_no, next revision.
  public BuildingPlan Revise(BuildingPlanStatus currentStatus, BuildingPlanStatus revisedStatus, BuildingPlanStatus submittedStatus, BuildingPlanId newId, Details details)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    revisedStatus.EnsureIs(SystemMasterCodes.Revised);
    submittedStatus.EnsureIs(SystemMasterCodes.Submitted);

    if (currentStatus.Id != BuildingPlanStatusId)
      throw new DomainException("The supplied status does not match the plan.");

    if (currentStatus.Is(SystemMasterCodes.Revised))
      throw new DomainException($"Revision {RevisionNo} of plan {PlanNo.Value} has already been revised; revise the latest revision.");

    var revision = new BuildingPlan
    {
      Id = newId,
      PropertyId = PropertyId,
      PlanNo = PlanNo,
      BuildingPlanStatusId = submittedStatus.Id,
      RevisionNo = RevisionNo + 1,
      SupersedesPlanId = Id
    };

    revision.Apply(details);
    BuildingPlanStatusId = revisedStatus.Id;
    return revision;
  }

  private void EnsureUnderConsideration(BuildingPlanStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != BuildingPlanStatusId)
      throw new DomainException("The supplied status does not match the plan.");

    if (IsClosed(currentStatus))
      throw new DomainException($"Plan {PlanNo.Value} rev. {RevisionNo} is {currentStatus.Name.Value}. Submit a revision to change it.");
  }

  /// Approved, rejected, revised and withdrawn plans are decided: no edit, approval or status change.
  public static bool IsClosed(BuildingPlanStatus status) =>
      ClosedStatusCodes.Any(status.Is);

  public static readonly IReadOnlyList<string> ClosedStatusCodes = new[]
  {
    SystemMasterCodes.Approved, SystemMasterCodes.Rejected, SystemMasterCodes.Revised, SystemMasterCodes.Withdrawn
  };

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(details.PlanType);

    if (details.PlanType.Id != BuildingPlanTypeId)
      details.PlanType.EnsureActive();

    details.Applicant?.EnsureActive();

    if (details.CoveredArea is not null && details.Unit is null)
      throw new DomainException("A covered area needs its measurement unit.");

    if (details.Unit is { } unit && unit.Id != MeasurementUnitId)
      unit.EnsureActive();

    if (details.Floors is < 0)
      throw new DomainException("Floors cannot be negative.");

    BuildingPlanTypeId = details.PlanType.Id;
    ApplicantOwnerId = details.Applicant?.Id;
    SubmissionDate = details.SubmissionDate;
    CoveredArea = details.CoveredArea is null ? null : decimal.Round(Guard.Positive(details.CoveredArea.Value, "Covered area"), 4);
    MeasurementUnitId = details.Unit?.Id;
    CoveredAreaBase = CoveredArea is { } covered && details.Unit is { } areaUnit ? areaUnit.ToBase(covered) : null;
    Floors = details.Floors;
    ArchitectName = Guard.Text(details.ArchitectName, 150, "Architect name");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }
}
