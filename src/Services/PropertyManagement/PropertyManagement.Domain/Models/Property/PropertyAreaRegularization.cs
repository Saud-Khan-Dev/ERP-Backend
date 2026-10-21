/// One additional-area case: land beyond the original area that GDA regularizes (Act s.6(4)(c)).
///
/// The case moves through its lifecycle (Applied → Pending → Regularized / Rejected) on this row, but the area itself
/// is never edited — a different area is a new case. Regularized and Rejected are decisions (an approver's act, dated
/// on or before today) and final: a decided case does not go back; only when its effect ends, its order reference and
/// its remarks can still be recorded. Only active, Regularized cases that are still in effect count toward the
/// property's area.
public class PropertyAreaRegularization : Aggregate<AreaRegularizationId>
{
  public const int ApprovedByMaxLength = 150;

  public PropertyId PropertyId { get; private set; } = default!;
  public decimal AdditionalArea { get; private set; }
  public MasterId MeasurementUnitId { get; private set; } = default!;
  public decimal AdditionalAreaBase { get; private set; }
  public RegularizationStatus RegularizationStatus { get; private set; }
  public DateOnly? ApplicationDate { get; private set; }
  public DateOnly? RegularizationDate { get; private set; }
  public string? OrderReferenceNo { get; private set; }
  /// The officer who approved it: their name as it stood then, and their HR record when picked from the staff list.
  public string? ApprovedBy { get; private set; }
  public Guid? ApprovedByEmployeeId { get; private set; }
  public DateOnly? EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public string? Remarks { get; private set; }
  public bool IsActive { get; private set; }

  /// What a case records when it is opened or moved along.
  public sealed record Decision(
      RegularizationStatus Status,
      DateOnly? RegularizationDate,
      string? OrderReferenceNo,
      string? ApprovedBy,
      Guid? ApprovedByEmployeeId,
      DateOnly? EffectiveFrom,
      DateOnly? EffectiveTo,
      string? Remarks);

  public static bool IsDecided(RegularizationStatus status) => status is RegularizationStatus.Regularized or RegularizationStatus.Rejected;

  public bool IsDecided() => IsDecided(RegularizationStatus);

  public static PropertyAreaRegularization Open(
      AreaRegularizationId id,
      Property property,
      MeasurementUnit unit,
      decimal additionalArea,
      DateOnly? applicationDate,
      Decision decision,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(unit);
    ArgumentNullException.ThrowIfNull(decision);
    property.EnsureActive();
    unit.EnsureActive();
    Guard.Positive(additionalArea, "Additional area");

    if (applicationDate > today)
      throw new DomainException($"The application date cannot be in the future ({applicationDate:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");

    var regularization = new PropertyAreaRegularization
    {
      Id = id,
      PropertyId = property.Id,
      AdditionalArea = decimal.Round(additionalArea, 4),
      MeasurementUnitId = unit.Id,
      AdditionalAreaBase = unit.ToBase(additionalArea),
      ApplicationDate = applicationDate,
      RegularizationStatus = RegularizationStatus.Applied,
      IsActive = true
    };

    regularization.Apply(decision with { EffectiveTo = null }, today);
    return regularization;
  }

  /// Moves the case along. A decided case keeps its decision: only the end of its effect, the order reference and the
  /// remarks can still be recorded.
  public void UpdateCase(Decision decision, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(decision);

    if (!IsActive)
      throw new DomainException("This regularization case was withdrawn and can no longer change. Open a new case instead.");

    if (IsDecided())
    {
      // what the decision fixed may be left out (it stays as it is), but not changed
      var approvedBy = Guard.Text(decision.ApprovedBy, ApprovedByMaxLength, "Approved by");
      if (decision.Status != RegularizationStatus
          || (decision.RegularizationDate is { } date && date != RegularizationDate)
          || (decision.EffectiveFrom is { } from && from != EffectiveFrom)
          || (decision.ApprovedByEmployeeId is { } employee && employee != ApprovedByEmployeeId)
          || (approvedBy is not null && !string.Equals(approvedBy, ApprovedBy, StringComparison.Ordinal)))
        throw new DomainException(RegularizationStatus == RegularizationStatus.Rejected
          ? "This case was rejected; the decision is final. Open a new case if the owner applies again."
          : "This case was regularized; the decision is final. Only when its effect ends, the order reference and the remarks can still change.");

      Guard.DateOrder(EffectiveFrom, decision.EffectiveTo, "Effective from", "Effective to");
      EffectiveTo = decision.EffectiveTo;
      OrderReferenceNo = Guard.Text(decision.OrderReferenceNo, 100, "Order reference no.") ?? OrderReferenceNo;
      Remarks = Guard.Text(decision.Remarks, 4000, "Remarks") ?? Remarks;
      return;
    }

    Apply(decision, today);
  }

  /// Withdraws a case entered in error; the row stays as history (is_active = false).
  public void Deactivate()
  {
    if (!IsActive)
      throw new DomainException("This regularization case is already withdrawn.");

    IsActive = false;
  }

  public bool CountsTowardArea(DateOnly asOf) =>
      IsActive
      && RegularizationStatus == RegularizationStatus.Regularized
      && (EffectiveFrom is null || EffectiveFrom <= asOf)
      && (EffectiveTo is null || EffectiveTo > asOf);

  private void Apply(Decision decision, DateOnly today)
  {
    var status = decision.Status;

    if (!Enum.IsDefined(status))
      throw new DomainException("Unknown regularization status.");

    if (status == RegularizationStatus.Regularized && decision.RegularizationDate is null)
      throw new DomainException("A regularized case needs its regularization date.");

    if (decision.RegularizationDate is { } decided && decided > today)
      throw new DomainException($"The regularization date cannot be in the future ({decided:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");

    Guard.DateOrder(ApplicationDate, decision.RegularizationDate, "Application date", "Regularization date");
    Guard.DateOrder(decision.EffectiveFrom, decision.EffectiveTo, "Effective from", "Effective to");

    RegularizationStatus = status;
    RegularizationDate = decision.RegularizationDate;
    OrderReferenceNo = Guard.Text(decision.OrderReferenceNo, 100, "Order reference no.");
    ApprovedBy = Guard.Text(decision.ApprovedBy, ApprovedByMaxLength, "Approved by");
    ApprovedByEmployeeId = decision.ApprovedByEmployeeId;
    EffectiveFrom = decision.EffectiveFrom ?? (status == RegularizationStatus.Regularized ? decision.RegularizationDate : null);
    EffectiveTo = decision.EffectiveTo;
    Remarks = Guard.Text(decision.Remarks, 4000, "Remarks");
  }
}
