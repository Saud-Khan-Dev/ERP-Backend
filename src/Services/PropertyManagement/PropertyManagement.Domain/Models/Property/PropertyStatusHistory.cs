/// One status the property was in, and for how long (Occupied → Encroached → Under Litigation ...).
/// The open row (effective_to = null) matches property.property_status_id.
public class PropertyStatusHistory : Entity<PropertyStatusHistoryId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public MasterId PropertyStatusId { get; private set; } = default!;
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }
  public string? Reason { get; private set; }
  public string? ReferenceNo { get; private set; }

  public bool IsOpen => EffectiveTo is null;

  internal static PropertyStatusHistory Open(
      PropertyStatusHistoryId id,
      PropertyId propertyId,
      MasterId statusId,
      DateOnly effectiveFrom,
      string? reason,
      string? referenceNo) => new()
  {
    Id = id,
    PropertyId = propertyId,
    PropertyStatusId = statusId,
    EffectiveFrom = effectiveFrom,
    Reason = Guard.Text(reason, 300, "Reason"),
    ReferenceNo = Guard.Text(referenceNo, 100, "Reference no.")
  };

  internal void Close(DateOnly effectiveTo)
  {
    if (!IsOpen)
      throw new DomainException("This status period is already closed.");

    if (effectiveTo < EffectiveFrom)
      throw new DomainException($"The new status cannot start before the current one ({EffectiveFrom:yyyy-MM-dd}).");

    EffectiveTo = effectiveTo;
  }
}
