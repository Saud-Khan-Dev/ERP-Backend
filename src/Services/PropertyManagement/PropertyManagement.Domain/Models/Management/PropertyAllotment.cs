/// One allotment of a plot to an allottee.
///
/// Allotment is not ownership (schema guide): if GDA later confirms the allottee as legal owner, a
/// property_ownership row is created with acquired_via_allotment_id pointing here. An allotment can be
/// cancelled and restored (Act s.6(4)(c)); an appeal against a cancellation is a property_appeal.
public class PropertyAllotment : Aggregate<AllotmentId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public OwnerId AllotteeOwnerId { get; private set; } = default!;
  /// ALT-00001, issued by the ALLOTMENT code sequence.
  public BusinessCode AllotmentNo { get; private set; } = default!;
  public MasterId AllotmentTypeId { get; private set; } = default!;
  public MasterId AllotmentStatusId { get; private set; } = default!;
  public DateOnly AllotmentDate { get; private set; }
  public DateOnly? EffectiveDate { get; private set; }
  public DateOnly? ExpiryDate { get; private set; }
  public string? AllotmentLetterRef { get; private set; }
  /// Construction deadline, purpose ...
  public string? Conditions { get; private set; }
  public DateOnly? CancellationDate { get; private set; }
  public string? CancellationReason { get; private set; }
  public string? CancellationOrderRef { get; private set; }
  public DateOnly? RestorationDate { get; private set; }
  public string? RestorationOrderRef { get; private set; }
  public string? Remarks { get; private set; }
  public bool IsActive { get; private set; }

  public static PropertyAllotment Allot(
      AllotmentId id,
      Property property,
      PropertyOwner allottee,
      BusinessCode allotmentNo,
      AllotmentType allotmentType,
      AllotmentStatus activeStatus,
      DateOnly allotmentDate,
      DateOnly? effectiveDate,
      DateOnly? expiryDate,
      string? allotmentLetterRef,
      string? conditions,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(allottee);
    ArgumentNullException.ThrowIfNull(allotmentNo);
    ArgumentNullException.ThrowIfNull(activeStatus);
    property.EnsureActive();
    allottee.EnsureActive();
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    var allotment = new PropertyAllotment
    {
      Id = id,
      PropertyId = property.Id,
      AllotteeOwnerId = allottee.Id,
      AllotmentNo = allotmentNo,
      AllotmentStatusId = activeStatus.Id,
      IsActive = true
    };

    allotment.Apply(allotmentType, allotmentDate, effectiveDate, expiryDate, allotmentLetterRef, conditions, remarks);
    return allotment;
  }

  /// Corrects the allotment's details. Status changes go through Cancel / Restore / ChangeStatus.
  public void Update(
      AllotmentType allotmentType,
      DateOnly allotmentDate,
      DateOnly? effectiveDate,
      DateOnly? expiryDate,
      string? allotmentLetterRef,
      string? conditions,
      string? remarks)
  {
    EnsureActive();
    Apply(allotmentType, allotmentDate, effectiveDate, expiryDate, allotmentLetterRef, conditions, remarks);
  }

  public void Cancel(AllotmentStatus cancelledStatus, DateOnly cancellationDate, string reason, string? orderRef)
  {
    ArgumentNullException.ThrowIfNull(cancelledStatus);
    EnsureActive();
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);

    if (AllotmentStatusId == cancelledStatus.Id)
      throw new DomainException($"Allotment {AllotmentNo.Value} is already cancelled.");

    if (cancellationDate < AllotmentDate)
      throw new DomainException("An allotment cannot be cancelled before it was made.");

    AllotmentStatusId = cancelledStatus.Id;
    CancellationDate = cancellationDate;
    CancellationReason = Guard.RequiredText(reason, 4000, "Cancellation reason");
    CancellationOrderRef = Guard.Text(orderRef, 100, "Cancellation order ref");
  }

  /// Reverses a cancellation (e.g. after an appeal is allowed). The cancellation stays on the row.
  public void Restore(AllotmentStatus cancelledStatus, AllotmentStatus restoredStatus, DateOnly restorationDate, string? orderRef)
  {
    ArgumentNullException.ThrowIfNull(cancelledStatus);
    ArgumentNullException.ThrowIfNull(restoredStatus);
    EnsureActive();
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);
    restoredStatus.EnsureIs(SystemMasterCodes.Restored);

    if (AllotmentStatusId != cancelledStatus.Id)
      throw new DomainException("Only a cancelled allotment can be restored.");

    if (restorationDate < CancellationDate)
      throw new DomainException("An allotment cannot be restored before it was cancelled.");

    AllotmentStatusId = restoredStatus.Id;
    RestorationDate = restorationDate;
    RestorationOrderRef = Guard.Text(orderRef, 100, "Restoration order ref");
  }

  /// Any other admin-defined status (Surrendered, Expired ...).
  public void ChangeStatus(AllotmentStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureActive();
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Cancelled) || status.Is(SystemMasterCodes.Restored))
      throw new DomainException("Use cancel / restore for these statuses, so the order details are recorded.");

    AllotmentStatusId = status.Id;
  }

  /// The allottee can only be confirmed as owner while the allotment is in force.
  public void EnsureCanConfirmOwnership(AllotmentStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    EnsureActive();

    if (currentStatus.Id != AllotmentStatusId)
      throw new DomainException("The supplied status does not match the allotment.");

    if (!currentStatus.Is(SystemMasterCodes.Active) && !currentStatus.Is(SystemMasterCodes.Restored))
      throw new DomainException($"A {currentStatus.Name.Value} allotment cannot be confirmed as ownership.");
  }

  public void Deactivate() => IsActive = false;

  private void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Allotment {AllotmentNo.Value} is inactive.");
  }

  private void Apply(
      AllotmentType allotmentType,
      DateOnly allotmentDate,
      DateOnly? effectiveDate,
      DateOnly? expiryDate,
      string? allotmentLetterRef,
      string? conditions,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(allotmentType);

    if (allotmentType.Id != AllotmentTypeId)
      allotmentType.EnsureActive();

    Guard.DateOrder(allotmentDate, effectiveDate, "Allotment date", "Effective date");
    Guard.DateOrder(effectiveDate ?? allotmentDate, expiryDate, "Effective date", "Expiry date");

    AllotmentTypeId = allotmentType.Id;
    AllotmentDate = allotmentDate;
    EffectiveDate = effectiveDate;
    ExpiryDate = expiryDate;
    AllotmentLetterRef = Guard.Text(allotmentLetterRef, 100, "Allotment letter ref");
    Conditions = Guard.Text(conditions, 4000, "Conditions");
    Remarks = Guard.Text(remarks, 4000, "Remarks");
  }
}
