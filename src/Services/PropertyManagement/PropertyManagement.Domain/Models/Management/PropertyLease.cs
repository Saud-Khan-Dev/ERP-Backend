/// One long-term lease agreement.
///
/// History, never overwrite (rule 2): the terms can only be edited while the lease is a DRAFT. Once in
/// force, a change of terms is a renewal — a new lease row pointing back through renewed_from_lease_id —
/// and the old one becomes RENEWED. Leases end by expiry, termination or cancellation (s.28-A).
/// Collecting lease money is the Tax Module's job.
public class PropertyLease : Aggregate<LeaseId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  public OwnerId LesseeOwnerId { get; private set; } = default!;
  /// LSE-00001, issued by the LEASE code sequence.
  public BusinessCode LeaseNo { get; private set; } = default!;
  public MasterId LeaseTypeId { get; private set; } = default!;
  public MasterId LeaseStatusId { get; private set; } = default!;
  public DateOnly LeaseStartDate { get; private set; }
  public DateOnly LeaseEndDate { get; private set; }
  public int? LeaseTermYears { get; private set; }
  public string? LeasePurpose { get; private set; }
  /// Premium / annual lease money.
  public decimal? LeaseAmount { get; private set; }
  public AmountFrequency? AmountFrequency { get; private set; }
  public decimal? SecurityDeposit { get; private set; }
  public string? AgreementReference { get; private set; }
  public DateOnly? AgreementDate { get; private set; }
  public bool IsRenewable { get; private set; }
  public LeaseId? RenewedFromLeaseId { get; private set; }
  public DateOnly? TerminationDate { get; private set; }
  public string? TerminationReason { get; private set; }
  public string? Remarks { get; private set; }

  public sealed record Terms(
      LeaseType LeaseType,
      DateOnly LeaseStartDate,
      DateOnly LeaseEndDate,
      int? LeaseTermYears,
      string? LeasePurpose,
      decimal? LeaseAmount,
      AmountFrequency? AmountFrequency,
      decimal? SecurityDeposit,
      string? AgreementReference,
      DateOnly? AgreementDate,
      bool IsRenewable,
      string? Remarks);

  /// A lease starts as DRAFT (terms still negotiable) or directly ACTIVE.
  public static PropertyLease Create(LeaseId id, Property property, PropertyOwner lessee, BusinessCode leaseNo, LeaseStatus initialStatus, Terms terms)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(lessee);
    ArgumentNullException.ThrowIfNull(leaseNo);
    ArgumentNullException.ThrowIfNull(initialStatus);
    property.EnsureActive();
    lessee.EnsureActive();

    if (!initialStatus.Is(SystemMasterCodes.Draft) && !initialStatus.Is(SystemMasterCodes.Active))
      throw new DomainException("A new lease starts as DRAFT or ACTIVE.");

    var lease = new PropertyLease
    {
      Id = id,
      PropertyId = property.Id,
      LesseeOwnerId = lessee.Id,
      LeaseNo = leaseNo,
      LeaseStatusId = initialStatus.Id
    };

    lease.Apply(terms);
    return lease;
  }

  public void UpdateTerms(LeaseStatus currentStatus, Terms terms)
  {
    EnsureCurrent(currentStatus);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Lease {LeaseNo.Value} is {currentStatus.Name.Value}: its terms are history now. Renew it to change them.");

    Apply(terms);
  }

  public void Activate(LeaseStatus currentStatus, LeaseStatus activeStatus)
  {
    EnsureCurrent(currentStatus);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException("Only a draft lease can be activated.");

    LeaseStatusId = activeStatus.Id;
  }

  /// Creates the renewing lease and marks this one RENEWED.
  public PropertyLease Renew(
      LeaseStatus currentStatus,
      LeaseStatus renewedStatus,
      LeaseStatus activeStatus,
      LeaseId newId,
      BusinessCode newLeaseNo,
      PropertyOwner lessee,
      Terms newTerms)
  {
    EnsureCurrent(currentStatus);
    renewedStatus.EnsureIs(SystemMasterCodes.Renewed);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (!currentStatus.Is(SystemMasterCodes.Active) && !currentStatus.Is(SystemMasterCodes.Expired))
      throw new DomainException($"A {currentStatus.Name.Value} lease cannot be renewed.");

    if (!IsRenewable)
      throw new DomainException($"Lease {LeaseNo.Value} is not renewable.");

    if (newTerms.LeaseStartDate < LeaseStartDate)
      throw new DomainException("The renewal cannot start before the lease it renews.");

    var renewal = new PropertyLease
    {
      Id = newId,
      PropertyId = PropertyId,
      LesseeOwnerId = lessee.Id,
      LeaseNo = newLeaseNo,
      LeaseStatusId = activeStatus.Id,
      RenewedFromLeaseId = Id
    };

    lessee.EnsureActive();
    renewal.Apply(newTerms);
    LeaseStatusId = renewedStatus.Id;
    return renewal;
  }

  public void Terminate(LeaseStatus currentStatus, LeaseStatus terminatedStatus, DateOnly terminationDate, string reason)
  {
    EnsureInForce(currentStatus);
    terminatedStatus.EnsureIs(SystemMasterCodes.Terminated);
    End(terminatedStatus, terminationDate, reason);
  }

  /// Rule 5: the third violation within the notice period cancels the lease (Act s.28-A).
  public void Cancel(LeaseStatus currentStatus, LeaseStatus cancelledStatus, DateOnly cancellationDate, string reason)
  {
    EnsureInForce(currentStatus);
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);
    End(cancelledStatus, cancellationDate, reason);
  }

  public void Expire(LeaseStatus currentStatus, LeaseStatus expiredStatus)
  {
    EnsureInForce(currentStatus);
    expiredStatus.EnsureIs(SystemMasterCodes.Expired);
    LeaseStatusId = expiredStatus.Id;
  }

  public bool IsInForce(LeaseStatus currentStatus) =>
      currentStatus.Id == LeaseStatusId && (currentStatus.Is(SystemMasterCodes.Active) || currentStatus.Is(SystemMasterCodes.Draft));

  private void End(LeaseStatus status, DateOnly date, string reason)
  {
    if (date < LeaseStartDate)
      throw new DomainException("A lease cannot end before it started.");

    LeaseStatusId = status.Id;
    TerminationDate = date;
    TerminationReason = Guard.RequiredText(reason, 4000, "Reason");
  }

  private void EnsureInForce(LeaseStatus currentStatus)
  {
    EnsureCurrent(currentStatus);

    if (!IsInForce(currentStatus))
      throw new DomainException($"Lease {LeaseNo.Value} is {currentStatus.Name.Value} and no longer in force.");
  }

  private void EnsureCurrent(LeaseStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != LeaseStatusId)
      throw new DomainException("The supplied status does not match the lease.");
  }

  private void Apply(Terms terms)
  {
    ArgumentNullException.ThrowIfNull(terms);
    ArgumentNullException.ThrowIfNull(terms.LeaseType);

    if (terms.LeaseType.Id != LeaseTypeId)
      terms.LeaseType.EnsureActive();

    if (terms.LeaseEndDate <= terms.LeaseStartDate)
      throw new DomainException("The lease must end after it starts.");

    if (terms.LeaseTermYears is <= 0)
      throw new DomainException("Lease term must be at least one year.");

    if (terms.AmountFrequency is { } frequency && !Enum.IsDefined(frequency))
      throw new DomainException("Unknown amount frequency.");

    LeaseTypeId = terms.LeaseType.Id;
    LeaseStartDate = terms.LeaseStartDate;
    LeaseEndDate = terms.LeaseEndDate;
    LeaseTermYears = terms.LeaseTermYears;
    LeasePurpose = Guard.Text(terms.LeasePurpose, 200, "Lease purpose");
    LeaseAmount = Guard.NotNegative(terms.LeaseAmount, "Lease amount");
    AmountFrequency = terms.AmountFrequency;
    SecurityDeposit = Guard.NotNegative(terms.SecurityDeposit, "Security deposit");
    AgreementReference = Guard.Text(terms.AgreementReference, 100, "Agreement reference");
    AgreementDate = terms.AgreementDate;
    IsRenewable = terms.IsRenewable;
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }
}
