/// One allotment of a plot to an allottee.
///
/// Allotment is not ownership (schema guide): if GDA later confirms the allottee as legal owner, a
/// property_ownership row is created with acquired_via_allotment_id pointing here. An allotment can be
/// cancelled and restored (Act s.6(4)(c)); an appeal against a cancellation is a property_appeal.
///
/// Status machine (by master code): ACTIVE / RESTORED are "in force" — at most one allotment per property.
///   in force → CANCELLED (cancel act, with the order) → RESTORED (restore act)
///   in force → SURRENDERED or EXPIRED (status act); nothing ever goes back to ACTIVE.
/// Cancelling or surrendering a confirmed allotment ends the ownership it produced in the same step; restoring
/// does not bring it back (the clerk confirms again).
public class PropertyAllotment : Aggregate<AllotmentId>
{
  /// Only property classified GDA_PROPERTY is GDA's to allot.
  public const string GdaPropertyClassificationCode = SystemMasterCodes.ClassificationGdaProperty;
  public const string SurrenderedCode = SystemMasterCodes.Surrendered;

  public PropertyId PropertyId { get; private set; } = default!;
  public OwnerId AllotteeOwnerId { get; private set; } = default!;
  /// ALT-00001, issued by the ALLOTMENT code sequence.
  public BusinessCode AllotmentNo { get; private set; } = default!;
  public MasterId AllotmentTypeId { get; private set; } = default!;
  public MasterId AllotmentStatusId { get; private set; } = default!;
  public DateOnly AllotmentDate { get; private set; }
  public DateOnly? EffectiveDate { get; private set; }
  /// The end of the allotment's term; also the recorded date when one without a term is marked expired.
  public DateOnly? ExpiryDate { get; private set; }
  public string? AllotmentLetterRef { get; private set; }
  /// Construction deadline, purpose ...
  public string? Conditions { get; private set; }
  public DateOnly? CancellationDate { get; private set; }
  public string? CancellationReason { get; private set; }
  public string? CancellationOrderRef { get; private set; }
  public DateOnly? RestorationDate { get; private set; }
  public string? RestorationOrderRef { get; private set; }
  /// The day the allottee gave the plot back (status SURRENDERED).
  public DateOnly? SurrenderDate { get; private set; }
  public string? Remarks { get; private set; }
  public bool IsActive { get; private set; }

  /// Another allotment already in force on the same property, with its allottee's name for the message.
  public sealed record InForce(PropertyAllotment Allotment, string AllotteeName);

  public static bool IsInForceStatus(AllotmentStatus status) =>
      status.Is(SystemMasterCodes.Active) || status.Is(SystemMasterCodes.Restored);

  public static PropertyAllotment Allot(
      AllotmentId id,
      Property property,
      PropertyClassification classification,
      PropertyOwner allottee,
      BusinessCode allotmentNo,
      AllotmentType allotmentType,
      AllotmentStatus activeStatus,
      DateOnly allotmentDate,
      DateOnly? effectiveDate,
      DateOnly? expiryDate,
      string? allotmentLetterRef,
      string? conditions,
      string? remarks,
      InForce? inForce,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(classification);
    ArgumentNullException.ThrowIfNull(allottee);
    ArgumentNullException.ThrowIfNull(allotmentNo);
    ArgumentNullException.ThrowIfNull(activeStatus);
    property.EnsureActive();
    allottee.EnsureActive();
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (classification.Id != property.PropertyClassificationId)
      throw new DomainException("The classification supplied is not the property's classification.");

    if (!classification.Is(GdaPropertyClassificationCode))
      throw new DomainException(
        $"Only GDA property can be allotted; {property.PropertyCode.Value} is classified as {classification.Name.Value}.");

    EnsureNoneInForce(inForce, except: null);
    EnsureNotFuture(allotmentDate, today, "The allotment date");

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
      Property property,
      AllotmentType allotmentType,
      DateOnly allotmentDate,
      DateOnly? effectiveDate,
      DateOnly? expiryDate,
      string? allotmentLetterRef,
      string? conditions,
      string? remarks,
      DateOnly today)
  {
    EnsureOn(property);
    EnsureActive();
    EnsureNotFuture(allotmentDate, today, "The allotment date");
    Apply(allotmentType, allotmentDate, effectiveDate, expiryDate, allotmentLetterRef, conditions, remarks);
  }

  /// Act s.6(4)(c). Only an allotment in force can be cancelled. If the allottee was confirmed as owner through
  /// it, that ownership ends on the cancellation date in the same step; the ended rows are returned.
  public IReadOnlyList<PropertyOwnership> Cancel(
      AllotmentStatus currentStatus,
      AllotmentStatus cancelledStatus,
      DateOnly cancellationDate,
      string reason,
      string? orderRef,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(cancelledStatus);
    ArgumentNullException.ThrowIfNull(record);
    EnsureOn(record.Property);
    EnsureActive();
    EnsureCurrent(currentStatus);
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);

    if (currentStatus.Is(SystemMasterCodes.Cancelled))
      throw new DomainException($"Allotment {AllotmentNo.Value} is already cancelled.");

    if (!IsInForceStatus(currentStatus))
      throw new DomainException(
        $"Allotment {AllotmentNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}; only an allotment in force can be cancelled.");

    if (cancellationDate < AllotmentDate)
      throw new DomainException("An allotment cannot be cancelled before it was made.");

    EnsureNotFuture(cancellationDate, record.Today, "The cancellation date");
    var text = Guard.RequiredText(reason, 4000, "Cancellation reason");
    var order = Guard.Text(orderRef, 100, "Cancellation order ref");

    var ended = EndConfirmedOwnership(cancellationDate, "cancellation", record);

    AllotmentStatusId = cancelledStatus.Id;
    CancellationDate = cancellationDate;
    CancellationReason = text;
    CancellationOrderRef = order;
    return ended;
  }

  /// Reverses a cancellation (e.g. after an appeal is allowed). The cancellation stays on the row. The ownership
  /// the cancellation ended is not reopened: the clerk confirms the allottee again.
  public void Restore(
      Property property,
      AllotmentStatus currentStatus,
      AllotmentStatus restoredStatus,
      DateOnly restorationDate,
      string? orderRef,
      InForce? inForce,
      DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(restoredStatus);
    EnsureOn(property);
    EnsureActive();
    EnsureCurrent(currentStatus);
    restoredStatus.EnsureIs(SystemMasterCodes.Restored);

    if (!currentStatus.Is(SystemMasterCodes.Cancelled))
      throw new DomainException(
        $"Only a cancelled allotment can be restored; {AllotmentNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}.");

    if (restorationDate < CancellationDate)
      throw new DomainException($"An allotment cannot be restored before it was cancelled ({CancellationDate:yyyy-MM-dd}).");

    EnsureNotFuture(restorationDate, today, "The restoration date");
    EnsureNoneInForce(inForce, except: Id);

    AllotmentStatusId = restoredStatus.Id;
    RestorationDate = restorationDate;
    RestorationOrderRef = Guard.Text(orderRef, 100, "Restoration order ref");
  }

  /// The generic status act: an allotment in force can be SURRENDERED (on a date, ending a confirmed
  /// ownership like a cancellation does) or EXPIRED (once its expiry date has passed; one without an expiry
  /// date records the date it expired). Nothing goes back to ACTIVE; cancel and restore have their own acts.
  public IReadOnlyList<PropertyOwnership> ChangeStatus(
      AllotmentStatus currentStatus,
      AllotmentStatus newStatus,
      DateOnly? date,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(newStatus);
    ArgumentNullException.ThrowIfNull(record);
    EnsureOn(record.Property);
    EnsureActive();
    EnsureCurrent(currentStatus);

    if (newStatus.Is(SystemMasterCodes.Cancelled))
      throw new DomainException("Use the cancel act, so the cancellation order is recorded.");

    if (newStatus.Is(SystemMasterCodes.Active) || newStatus.Is(SystemMasterCodes.Restored))
      throw new DomainException(
        "An allotment cannot be put back in force through a status change; a cancelled allotment is restored with the restore act.");

    if (!newStatus.Is(SurrenderedCode) && !newStatus.Is(SystemMasterCodes.Expired))
      throw new DomainException($"An allotment can only be surrendered or expired here, not set to '{newStatus.Name.Value}'.");

    if (!IsInForceStatus(currentStatus))
      throw new DomainException(
        $"Allotment {AllotmentNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}; only an allotment in force can be surrendered or expired.");

    newStatus.EnsureActive();
    IReadOnlyList<PropertyOwnership> ended = Array.Empty<PropertyOwnership>();

    if (newStatus.Is(SurrenderedCode))
    {
      var surrendered = date ?? throw new DomainException("Give the date the allotment was surrendered.");
      EnsureWithinTerm(surrendered, record.Today, "surrender");
      ended = EndConfirmedOwnership(surrendered, "surrender", record);
      SurrenderDate = surrendered;
    }
    else if (ExpiryDate is { } expiry)
    {
      if (expiry >= record.Today)
        throw new DomainException($"{AllotmentNo.Value} expires on {expiry:yyyy-MM-dd}; it can be marked expired after that.");
    }
    else
    {
      var expired = date ?? throw new DomainException($"{AllotmentNo.Value} has no expiry date; give the date it expired.");
      EnsureWithinTerm(expired, record.Today, "expiry");
      ExpiryDate = expired;
    }

    AllotmentStatusId = newStatus.Id;
    return ended;
  }

  /// GDA confirms the allottee as legal owner (the only way an allotment leads to ownership). The allotment
  /// must be in force and not confirmed yet; ownership starts on or after the allotment (or restoration) date
  /// and not in the future, and only a share nobody holds can be confirmed.
  public PropertyOwnership ConfirmOwnership(
      OwnershipId id,
      AllotmentStatus currentStatus,
      PropertyOwner allottee,
      TenureType tenureType,
      decimal sharePct,
      DateOnly effectiveFrom,
      string? referenceNo,
      string? remarks,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(allottee);
    ArgumentNullException.ThrowIfNull(record);
    EnsureOn(record.Property);
    EnsureCanConfirmOwnership(currentStatus);

    if (ConfirmedOwnership(record.Ownerships) is { } confirmed)
      throw new DomainException(
        $"{AllotmentNo.Value} was already confirmed as ownership from {confirmed.EffectiveFrom:yyyy-MM-dd} ({confirmed.OwnershipSharePct:0.####}%).");

    if (effectiveFrom < AllotmentDate)
      throw new DomainException(
        $"Ownership through {AllotmentNo.Value} cannot start before the allotment date ({AllotmentDate:yyyy-MM-dd}).");

    if (RestorationDate is { } restored && effectiveFrom < restored)
      throw new DomainException($"{AllotmentNo.Value} was restored on {restored:yyyy-MM-dd}; confirm ownership from that date or later.");

    EnsureNotFuture(effectiveFrom, record.Today, "The ownership start date");

    if (OwnershipShares.Total(record.Current) >= OwnershipShares.Whole)
      throw new DomainException(
        $"Someone already holds 100% of {record.Property.PropertyCode.Value}; transfer a share to the allottee instead.");

    PropertyOwnership.EnsureCanOpen(allottee, sharePct, effectiveFrom, record);

    return PropertyOwnership.FromAllotment(id, record.Property, this, allottee, tenureType, sharePct, effectiveFrom,
      referenceNo ?? AllotmentLetterRef, remarks ?? $"Confirmed from allotment {AllotmentNo.Value}.", record.Today);
  }

  /// The allottee can only be confirmed as owner while the allotment is in force.
  public void EnsureCanConfirmOwnership(AllotmentStatus currentStatus)
  {
    EnsureActive();
    EnsureCurrent(currentStatus);

    if (!IsInForceStatus(currentStatus))
      throw new DomainException($"A {currentStatus.Name.Value} allotment cannot be confirmed as ownership.");
  }

  /// The ownership confirmed through this allotment since it was last put in force (the current row first —
  /// the remainder after a partial sale carries the allotment too — else the latest), or null when the
  /// allottee has not been confirmed. Rows ended by a cancellation before a restoration do not count.
  public PropertyOwnership? ConfirmedOwnership(IEnumerable<PropertyOwnership> ownerships) =>
      ownerships
        .Where(o => o.AcquiredViaAllotmentId == Id && (RestorationDate is null || o.EffectiveFrom >= RestorationDate))
        .OrderByDescending(o => o.IsCurrent)
        .ThenByDescending(o => o.EffectiveFrom)
        .FirstOrDefault();

  /// Withdraws an allotment entered in error. Refused while the allottee holds ownership confirmed through it.
  public void Deactivate(Property property, IEnumerable<PropertyOwnership> ownerships)
  {
    EnsureOn(property);
    EnsureActive();

    if (ConfirmedOwnership(ownerships) is { IsCurrent: true } confirmed)
      throw new DomainException(
        $"{AllotmentNo.Value} was confirmed as ownership ({confirmed.OwnershipSharePct:0.####}% from {confirmed.EffectiveFrom:yyyy-MM-dd}); cancel the allotment or end that ownership first.");

    IsActive = false;
  }

  /// Ends the current ownership rows produced by this allotment (the confirmation and a remainder after a
  /// partial sale) on the given date, under the same rules as ending an ownership by hand.
  private IReadOnlyList<PropertyOwnership> EndConfirmedOwnership(DateOnly date, string act, OwnershipRecord record)
  {
    var rows = record.Current.Where(o => o.AcquiredViaAllotmentId == Id).ToList();

    foreach (var row in rows)
    {
      if (date < row.EffectiveFrom)
        throw new DomainException(
          $"{AllotmentNo.Value} was confirmed as ownership from {row.EffectiveFrom:yyyy-MM-dd}; date the {act} on or after that.");

      row.EnsureCanEnd(record, $"the allotment is {(act == "cancellation" ? "cancelled" : "surrendered")}");
    }

    foreach (var row in rows)
      row.End(date, $"Ended by {act} of {AllotmentNo.Value}.");

    return rows;
  }

  private static void EnsureNoneInForce(InForce? inForce, AllotmentId? except)
  {
    if (inForce is not null && inForce.Allotment.Id != except)
      throw new DomainException(
        $"{inForce.Allotment.AllotmentNo.Value} to {inForce.AllotteeName} is already in force on this plot; cancel, surrender or expire it first.");
  }

  private static void EnsureNotFuture(DateOnly date, DateOnly today, string what)
  {
    if (date > today)
      throw new DomainException($"{what} cannot be in the future ({date:yyyy-MM-dd}); today is {today:yyyy-MM-dd}.");
  }

  private void EnsureWithinTerm(DateOnly date, DateOnly today, string act)
  {
    if (date < AllotmentDate)
      throw new DomainException($"The {act} cannot be dated before the allotment ({AllotmentDate:yyyy-MM-dd}).");

    EnsureNotFuture(date, today, $"The {act} date");
  }

  private void EnsureCurrent(AllotmentStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != AllotmentStatusId)
      throw new DomainException("The supplied status does not match the allotment.");
  }

  private void EnsureOn(Property property)
  {
    ArgumentNullException.ThrowIfNull(property);

    if (property.Id != PropertyId)
      throw new DomainException("The allotment belongs to another property.");

    property.EnsureActive();
  }

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
