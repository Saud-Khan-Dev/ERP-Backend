/// One additional-area case: land beyond the original area that GDA regularizes (Act s.6(4)(c)).
///
/// The case moves through its lifecycle (Applied → Pending → Regularized / Rejected) on this row, but
/// the area itself is never edited — a different area is a new case. Only active, Regularized cases
/// that are still in effect count toward the property's area.
public class PropertyAreaRegularization : Aggregate<AreaRegularizationId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public decimal AdditionalArea { get; private set; }
  public MasterId MeasurementUnitId { get; private set; } = default!;
  public decimal AdditionalAreaBase { get; private set; }
  public RegularizationStatus RegularizationStatus { get; private set; }
  public DateOnly? ApplicationDate { get; private set; }
  public DateOnly? RegularizationDate { get; private set; }
  public string? OrderReferenceNo { get; private set; }
  public string? ApprovedBy { get; private set; }
  public DateOnly? EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public string? Remarks { get; private set; }
  public bool IsActive { get; private set; }

  public static PropertyAreaRegularization Open(
      AreaRegularizationId id,
      Property property,
      MeasurementUnit unit,
      decimal additionalArea,
      RegularizationStatus status,
      DateOnly? applicationDate,
      DateOnly? regularizationDate,
      string? orderReferenceNo,
      string? approvedBy,
      DateOnly? effectiveFrom,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(unit);
    property.EnsureActive();
    unit.EnsureActive();
    Guard.Positive(additionalArea, "Additional area");

    var regularization = new PropertyAreaRegularization
    {
      Id = id,
      PropertyId = property.Id,
      AdditionalArea = decimal.Round(additionalArea, 4),
      MeasurementUnitId = unit.Id,
      AdditionalAreaBase = unit.ToBase(additionalArea),
      ApplicationDate = applicationDate,
      IsActive = true
    };

    regularization.Apply(status, regularizationDate, orderReferenceNo, approvedBy, effectiveFrom, effectiveTo: null, remarks);
    return regularization;
  }

  public void UpdateCase(
      RegularizationStatus status,
      DateOnly? regularizationDate,
      string? orderReferenceNo,
      string? approvedBy,
      DateOnly? effectiveFrom,
      DateOnly? effectiveTo,
      string? remarks)
  {
    if (!IsActive)
      throw new DomainException("This regularization case is inactive.");

    Apply(status, regularizationDate, orderReferenceNo, approvedBy, effectiveFrom, effectiveTo, remarks);
  }

  public void Deactivate() => IsActive = false;

  public bool CountsTowardArea(DateOnly asOf) =>
      IsActive
      && RegularizationStatus == RegularizationStatus.Regularized
      && (EffectiveFrom is null || EffectiveFrom <= asOf)
      && (EffectiveTo is null || EffectiveTo > asOf);

  private void Apply(
      RegularizationStatus status,
      DateOnly? regularizationDate,
      string? orderReferenceNo,
      string? approvedBy,
      DateOnly? effectiveFrom,
      DateOnly? effectiveTo,
      string? remarks)
  {
    if (!Enum.IsDefined(status))
      throw new DomainException("Unknown regularization status.");

    if (status == RegularizationStatus.Regularized && regularizationDate is null)
      throw new DomainException("A regularized case needs its regularization date.");

    Guard.DateOrder(ApplicationDate, regularizationDate, "Application date", "Regularization date");
    Guard.DateOrder(effectiveFrom, effectiveTo, "Effective from", "Effective to");

    RegularizationStatus = status;
    RegularizationDate = regularizationDate;
    OrderReferenceNo = Guard.Text(orderReferenceNo, 100, "Order reference no.");
    ApprovedBy = Guard.Text(approvedBy, 150, "Approved by");
    EffectiveFrom = effectiveFrom ?? (status == RegularizationStatus.Regularized ? regularizationDate : null);
    EffectiveTo = effectiveTo;
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }
}
