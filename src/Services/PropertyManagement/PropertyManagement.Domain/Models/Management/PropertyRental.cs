/// One rent agreement. Lease vs rental follows GDA terminology (req. §19); rent collection belongs to
/// the Tax / Finance Module. A renewal is a new rental row (renewed_from_rental_id), the old one ENDED.
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

  public static PropertyRental Create(RentalId id, Property property, PropertyOwner tenant, BusinessCode rentalNo, RentalStatus activeStatus, Terms terms)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(tenant);
    ArgumentNullException.ThrowIfNull(rentalNo);
    ArgumentNullException.ThrowIfNull(activeStatus);
    property.EnsureActive();
    tenant.EnsureActive();
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    var rental = new PropertyRental
    {
      Id = id,
      PropertyId = property.Id,
      TenantOwnerId = tenant.Id,
      RentalNo = rentalNo,
      RentalStatusId = activeStatus.Id
    };

    rental.Apply(terms);
    return rental;
  }

  public void UpdateTerms(RentalStatus currentStatus, Terms terms)
  {
    EnsureInForce(currentStatus);
    Apply(terms);
  }

  public PropertyRental Renew(RentalStatus currentStatus, RentalStatus endedStatus, RentalStatus activeStatus, RentalId newId, BusinessCode newRentalNo, PropertyOwner tenant, Terms newTerms)
  {
    EnsureInForce(currentStatus);
    endedStatus.EnsureIs(SystemMasterCodes.Ended);
    activeStatus.EnsureIs(SystemMasterCodes.Active);
    tenant.EnsureActive();

    if (newTerms.RentalStartDate < RentalStartDate)
      throw new DomainException("The renewal cannot start before the rental it renews.");

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
    RentalStatusId = endedStatus.Id;
    RentalEndDate ??= newTerms.RentalStartDate;
    return renewal;
  }

  public void Terminate(RentalStatus currentStatus, RentalStatus endedStatus, DateOnly terminationDate, string reason)
  {
    EnsureInForce(currentStatus);
    endedStatus.EnsureIs(SystemMasterCodes.Ended);
    End(endedStatus, terminationDate, reason);
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
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != RentalStatusId)
      throw new DomainException("The supplied status does not match the rental.");

    if (!IsInForce(currentStatus))
      throw new DomainException($"Rental {RentalNo.Value} is {currentStatus.Name.Value} and no longer in force.");
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
