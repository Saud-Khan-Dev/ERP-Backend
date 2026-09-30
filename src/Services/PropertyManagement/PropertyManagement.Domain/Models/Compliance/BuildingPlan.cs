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
  public string? ApprovedBy { get; private set; }
  public string? ApprovalReferenceNo { get; private set; }
  public DateOnly? ValidityEndDate { get; private set; }
  public decimal? CoveredArea { get; private set; }
  public MasterId? MeasurementUnitId { get; private set; }
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

  public void Approve(BuildingPlanStatus currentStatus, BuildingPlanStatus approvedStatus, DateOnly approvalDate, string approvedBy, string? approvalReferenceNo, DateOnly? validityEndDate)
  {
    EnsureUnderConsideration(currentStatus);
    approvedStatus.EnsureIs(SystemMasterCodes.Approved);
    Guard.DateOrder(SubmissionDate, approvalDate, "Submission date", "Approval date");
    Guard.DateOrder(approvalDate, validityEndDate, "Approval date", "Validity end date");

    BuildingPlanStatusId = approvedStatus.Id;
    ApprovalDate = approvalDate;
    ApprovedBy = Guard.RequiredText(approvedBy, 150, "Approved by");
    ApprovalReferenceNo = Guard.Text(approvalReferenceNo, 100, "Approval reference no.");
    ValidityEndDate = validityEndDate;
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

    if (currentStatus.Is(SystemMasterCodes.Approved) || currentStatus.Is(SystemMasterCodes.Rejected) || currentStatus.Is(SystemMasterCodes.Revised))
      throw new DomainException($"Plan {PlanNo.Value} rev. {RevisionNo} is {currentStatus.Name.Value}. Submit a revision to change it.");
  }

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
    Floors = details.Floors;
    ArchitectName = Guard.Text(details.ArchitectName, 150, "Architect name");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }
}
