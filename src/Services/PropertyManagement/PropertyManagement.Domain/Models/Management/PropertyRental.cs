/// One rent agreement. Lease vs rental follows GDA terminology (req. §19); rent collection belongs to
/// the Tax / Finance Module.
///
/// History, never overwrite (rule 2): a DRAFT takes any change; once ACTIVE the terms (rent, dates, increase, deposit)
/// are history and only the agreement reference, its date and the remarks can be corrected - new terms are a renewal,
/// a new rental row (renewed_from_rental_id) after which the old one is ENDED (the day before the renewal starts).
/// Ends: ENDED (ran its course or replaced by its renewal), EXPIRED (marked once the end date passed), TERMINATED
/// (ended early by an order - an approver's act, like a lease's), CANCELLED (rule 5, s.28-A).
public class PropertyRental : Aggregate<RentalId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public OwnerId TenantOwnerId { get; private set; } = default!;
  /// RNT-00001, issued by the RENTAL code sequence.
  public BusinessCode RentalNo { get; private set; } = default!;
  public MasterId RentalTypeId { get; private set; } = default!;
  public MasterId RentalStatusId { get; private set; } = default!;
  public DateOnly RentalStartDate { get; private set; }
  public DateOnly? RentalEndDate { get; private set; }
  public decimal RentAmount { get; private set; }
  public RentFrequency RentFrequency { get; private set; }
  public decimal? SecurityDeposit { get; private set; }
  public decimal? AnnualIncreasePct { get; private set; }
  public string? AgreementReference { get; private set; }
  public DateOnly? AgreementDate { get; private set; }
  public RentalId? RenewedFromRentalId { get; private set; }
  public DateOnly? TerminationDate { get; private set; }
  public string? TerminationReason { get; private set; }
  public string? Remarks { get; private set; }

  public sealed record Terms(
      RentalType RentalType,
      DateOnly RentalStartDate,
      DateOnly? RentalEndDate,
      decimal RentAmount,
      RentFrequency RentFrequency,
      decimal? SecurityDeposit,
      decimal? AnnualIncreasePct,
      string? AgreementReference,
      DateOnly? AgreementDate,
      string? Remarks);

  /// A rental starts as a DRAFT (terms still being agreed) or directly ACTIVE.
  public static PropertyRental Create(RentalId id, Property property, PropertyOwner tenant, BusinessCode rentalNo, RentalStatus initialStatus, Terms terms)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(tenant);
    ArgumentNullException.ThrowIfNull(rentalNo);
    ArgumentNullException.ThrowIfNull(initialStatus);
    property.EnsureActive();
    tenant.EnsureActive();

    if (!initialStatus.Is(SystemMasterCodes.Draft) && !initialStatus.Is(SystemMasterCodes.Active))
      throw new DomainException("A new rental starts as a draft or active.");

    var rental = new PropertyRental
    {
      Id = id,
      PropertyId = property.Id,
      TenantOwnerId = tenant.Id,
      RentalNo = rentalNo,
      RentalStatusId = initialStatus.Id
    };

    rental.Apply(terms);
    return rental;
  }

  /// A draft takes any change; an active rental only corrections of the agreement reference, its date and the remarks.
  public void UpdateTerms(RentalStatus currentStatus, Terms terms)
  {
    EnsureCurrent(currentStatus);
    ArgumentNullException.ThrowIfNull(terms);

    if (currentStatus.Is(SystemMasterCodes.Draft))
    {
      Apply(terms);
      return;
    }

    EnsureInForce(currentStatus);

    var termsChanged = terms.RentalType.Id != RentalTypeId || terms.RentalStartDate != RentalStartDate || terms.RentalEndDate != RentalEndDate
      || terms.RentAmount != RentAmount || terms.RentFrequency != RentFrequency || terms.SecurityDeposit != SecurityDeposit
      || terms.AnnualIncreasePct != AnnualIncreasePct;

    if (termsChanged)
      throw new DomainException(
        $"Rental {RentalNo.Value} is in force, so its terms are history: renew or revise it to change the rent, dates or increase. Only the agreement reference, its date and the remarks can be corrected.");

    AgreementReference = Guard.Text(terms.AgreementReference, 100, "Agreement reference");
    AgreementDate = terms.AgreementDate;
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }

  /// A draft comes into force.
  public void Activate(RentalStatus currentStatus, RentalStatus activeStatus)
  {
    EnsureCurrent(currentStatus);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Only a draft rental can be activated; {RentalNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}.");

    RentalStatusId = activeStatus.Id;
  }

  /// New terms, or the same tenant again after the end: a new rental row; an active one is ENDED the day before the
  /// renewal starts, an expired one keeps its status.
  public PropertyRental Renew(RentalStatus currentStatus, RentalStatus endedStatus, RentalStatus activeStatus, RentalId newId, BusinessCode newRentalNo, PropertyOwner tenant, Terms newTerms)
  {
    EnsureCurrent(currentStatus);
    if (!currentStatus.Is(SystemMasterCodes.Active) && !currentStatus.Is(SystemMasterCodes.Expired))
      throw new DomainException($"Rental {RentalNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}; only an active or expired rental can be renewed.");
    endedStatus.EnsureIs(SystemMasterCodes.Ended);
    activeStatus.EnsureIs(SystemMasterCodes.Active);
    tenant.EnsureActive();

    ArgumentNullException.ThrowIfNull(newTerms);

    if (newTerms.RentalStartDate <= RentalStartDate)
      throw new DomainException($"The renewal must start after {RentalNo.Value} started ({RentalStartDate:yyyy-MM-dd}).");

    var renewal = new PropertyRental
    {
      Id = newId,
      PropertyId = PropertyId,
      TenantOwnerId = tenant.Id,
      RentalNo = newRentalNo,
      RentalStatusId = activeStatus.Id,
      RenewedFromRentalId = Id
    };

    renewal.Apply(newTerms);

    if (currentStatus.Is(SystemMasterCodes.Active))
    {
      RentalStatusId = endedStatus.Id;

      // the two rentals never overlap: this one ends the day before the renewal starts
      var lastDay = newTerms.RentalStartDate.AddDays(-1);
      if (RentalEndDate is null || RentalEndDate > lastDay)
        RentalEndDate = lastDay;
    }

    return renewal;
  }

  /// Marked once its end date has passed. An open-ended rental never expires (terminate it).
  public void Expire(RentalStatus currentStatus, RentalStatus expiredStatus, DateOnly today)
  {
    EnsureInForce(currentStatus);
    expiredStatus.EnsureIs(SystemMasterCodes.Expired);

    if (RentalEndDate is not { } end)
      throw new DomainException($"{RentalNo.Value} has no end date, so it cannot expire. Terminate it, or renew it with an end date.");

    if (today <= end)
      throw new DomainException($"{RentalNo.Value} ends on {end:yyyy-MM-dd}; it can be marked expired after that.");

    RentalStatusId = expiredStatus.Id;
  }

  /// The period the rental occupies the property, for the overlap rule.
  public AgreementSpan Span(string tenantName) =>
      new(AgreementSpanKind.Rental, RentalNo.Value, tenantName, RentalStartDate, TerminationDate ?? RentalEndDate, RentalTypeId);

  /// The rent due per period on a day: the agreed rent raised by the annual increase once for every full year since
  /// the rental started (to its end at most). Before the start, the agreed rent.
  public decimal CurrentRent(DateOnly asOf)
  {
    if (AnnualIncreasePct is not { } pct || pct <= 0)
      return RentAmount;

    var until = RentalEndDate is { } end && end < asOf ? end : asOf;
    var years = FullYears(RentalStartDate, until);
    if (years <= 0)
      return RentAmount;

    var factor = (decimal)Math.Pow((double)(1 + pct / 100m), years);
    return decimal.Round(RentAmount * factor, 2);
  }

  private static int FullYears(DateOnly from, DateOnly to)
  {
    if (to < from)
      return 0;

    var years = to.Year - from.Year;
    if (from.AddYears(years) > to)
      years--;

    return years;
  }

  /// An appeal set aside the violation that cancelled this rental (Act s.32): the rental is in force again.
  public void Reinstate(RentalStatus currentStatus, RentalStatus activeStatus, string appealNo)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (currentStatus.Id != RentalStatusId)
      throw new DomainException("The supplied status does not match the rental.");

    if (!currentStatus.Is(SystemMasterCodes.Cancelled))
      throw new DomainException($"{RentalNo.Value} is {currentStatus.Name.Value}; only a cancelled rental can be reinstated.");

    var cancelledOn = TerminationDate;
    RentalStatusId = activeStatus.Id;
    TerminationDate = null;
    TerminationReason = null;
    Remarks = RemarkText.Append(Remarks, $"Reinstated by appeal {appealNo} (cancellation of {cancelledOn:yyyy-MM-dd} set aside).");
  }

  /// Ends the rental early by an order (TERMINATED): not in the future, not after its agreed end.
  public void Terminate(RentalStatus currentStatus, RentalStatus terminatedStatus, DateOnly terminationDate, string reason, DateOnly? today = null)
  {
    EnsureInForce(currentStatus);
    terminatedStatus.EnsureIs(SystemMasterCodes.Terminated);
    AgreementDates.EnsureEndedOn(RentalNo.Value, terminationDate, RentalStartDate, RentalEndDate, today);
    RentalStatusId = terminatedStatus.Id;
    TerminationDate = terminationDate;
    TerminationReason = Guard.RequiredText(reason, 4000, "Reason");
  }

  /// A draft that will not go ahead is withdrawn (WITHDRAWN, with the reason): it never came into force.
  public void WithdrawDraft(RentalStatus currentStatus, RentalStatus withdrawnStatus, string reason)
  {
    EnsureCurrent(currentStatus);
    withdrawnStatus.EnsureIs(SystemMasterCodes.Withdrawn);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Only a draft can be withdrawn; {RentalNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}. Terminate it instead.");

    RentalStatusId = withdrawnStatus.Id;
    TerminationReason = Guard.RequiredText(reason, 4000, "Reason");
  }

  /// Rule 5: the third violation within the notice period cancels the rental (Act s.28-A).
  public void Cancel(RentalStatus currentStatus, RentalStatus cancelledStatus, DateOnly cancellationDate, string reason)
  {
    EnsureInForce(currentStatus);
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);
    End(cancelledStatus, cancellationDate, reason);
  }

  public bool IsInForce(RentalStatus currentStatus) =>
      currentStatus.Id == RentalStatusId && currentStatus.Is(SystemMasterCodes.Active);

  private void End(RentalStatus status, DateOnly date, string reason)
  {
    if (date < RentalStartDate)
      throw new DomainException("A rental cannot end before it started.");

    RentalStatusId = status.Id;
    TerminationDate = date;
    TerminationReason = Guard.RequiredText(reason, 4000, "Reason");
  }

  private void EnsureInForce(RentalStatus currentStatus)
  {
    EnsureCurrent(currentStatus);

    if (currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Rental {RentalNo.Value} is still a draft; activate it first.");

    if (!IsInForce(currentStatus))
      throw new DomainException($"Rental {RentalNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()} and no longer in force.");
  }

  private void EnsureCurrent(RentalStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != RentalStatusId)
      throw new DomainException("The supplied status does not match the rental.");
  }

  private void Apply(Terms terms)
  {
    ArgumentNullException.ThrowIfNull(terms);
    ArgumentNullException.ThrowIfNull(terms.RentalType);

    if (terms.RentalType.Id != RentalTypeId)
      terms.RentalType.EnsureActive();

    if (!Enum.IsDefined(terms.RentFrequency))
      throw new DomainException("Unknown rent frequency.");

    Guard.DateOrder(terms.RentalStartDate, terms.RentalEndDate, "Rental start date", "Rental end date");

    if (terms.AnnualIncreasePct is < 0 or > 999.99m)
      throw new DomainException("Annual increase must be between 0 and 999.99%.");

    RentalTypeId = terms.RentalType.Id;
    RentalStartDate = terms.RentalStartDate;
    RentalEndDate = terms.RentalEndDate;
    RentAmount = Guard.NotNegative(terms.RentAmount, "Rent amount");
    RentFrequency = terms.RentFrequency;
    SecurityDeposit = Guard.NotNegative(terms.SecurityDeposit, "Security deposit");
    AnnualIncreasePct = terms.AnnualIncreasePct;
    AgreementReference = Guard.Text(terms.AgreementReference, 100, "Agreement reference");
    AgreementDate = terms.AgreementDate;
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }
}
