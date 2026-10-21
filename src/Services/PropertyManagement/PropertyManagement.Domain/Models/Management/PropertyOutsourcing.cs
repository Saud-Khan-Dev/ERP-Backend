/// One contract handing the property (or a service on it) to a contractor.
///
/// History, never overwrite (rule 2): a DRAFT takes any change; once ACTIVE the terms (type, dates, amount, guarantee)
/// are history and only the reference no. and the remarks can be corrected (and the money direction recorded when it
/// was never set) - new terms are a renewal, a new contract row (renewed_from_contract_id) after which the old one is
/// RENEWED (ending the day before the renewal starts). Ends: EXPIRED (marked once the end date passed), TERMINATED
/// (ended early by an order - an approver's act). Status changes only through these acts.
public class PropertyOutsourcing : Aggregate<OutsourcingId>
{
  public PropertyId PropertyId { get; private set; } = default!;
  /// The contractor — a property_owner row, usually an organization.
  public OwnerId OutsourcedPartyOwnerId { get; private set; } = default!;
  /// CON-00001, issued by the CONTRACT code sequence.
  public BusinessCode ContractNo { get; private set; } = default!;
  public MasterId OutsourcingTypeId { get; private set; } = default!;
  public MasterId ContractStatusId { get; private set; } = default!;
  public DateOnly ContractStartDate { get; private set; }
  public DateOnly? ContractEndDate { get; private set; }
  public string? PurposeService { get; private set; }
  public decimal? ContractAmount { get; private set; }
  public AmountFrequency? AmountFrequency { get; private set; }
  /// Who pays whom: the contractor GDA (receivable, rent-roll income) or GDA the contractor (payable, an expense).
  public MoneyDirection? MoneyDirection { get; private set; }
  public decimal? PerformanceGuarantee { get; private set; }
  public string? ReferenceNo { get; private set; }
  public OutsourcingId? RenewedFromContractId { get; private set; }
  public DateOnly? TerminationDate { get; private set; }
  public string? TerminationReason { get; private set; }
  public string? Remarks { get; private set; }

  public sealed record Terms(
      OutsourcingType OutsourcingType,
      DateOnly ContractStartDate,
      DateOnly? ContractEndDate,
      string? PurposeService,
      decimal? ContractAmount,
      AmountFrequency? AmountFrequency,
      decimal? PerformanceGuarantee,
      string? ReferenceNo,
      string? Remarks,
      MoneyDirection? MoneyDirection = null);

  /// A contract starts as a DRAFT (terms still being agreed) or directly ACTIVE.
  public static PropertyOutsourcing Create(OutsourcingId id, Property property, PropertyOwner contractor, BusinessCode contractNo, ContractStatus initialStatus, Terms terms)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(contractor);
    ArgumentNullException.ThrowIfNull(contractNo);
    ArgumentNullException.ThrowIfNull(initialStatus);
    property.EnsureActive();
    contractor.EnsureActive();

    if (!initialStatus.Is(SystemMasterCodes.Draft) && !initialStatus.Is(SystemMasterCodes.Active))
      throw new DomainException("A new contract starts as a draft or active.");

    var contract = new PropertyOutsourcing
    {
      Id = id,
      PropertyId = property.Id,
      OutsourcedPartyOwnerId = contractor.Id,
      ContractNo = contractNo,
      ContractStatusId = initialStatus.Id
    };

    contract.Apply(terms);
    return contract;
  }

  /// A draft takes any change; an active contract only corrections of the reference no. and the remarks, and the money
  /// direction when it was never recorded.
  public void Update(ContractStatus currentStatus, Terms terms)
  {
    EnsureCurrent(currentStatus);
    ArgumentNullException.ThrowIfNull(terms);

    if (currentStatus.Is(SystemMasterCodes.Draft))
    {
      Apply(terms);
      return;
    }

    EnsureInForce(currentStatus);

    var termsChanged = terms.OutsourcingType.Id != OutsourcingTypeId || terms.ContractStartDate != ContractStartDate
      || terms.ContractEndDate != ContractEndDate || terms.ContractAmount != ContractAmount || terms.AmountFrequency != AmountFrequency
      || terms.PerformanceGuarantee != PerformanceGuarantee
      || !string.Equals(Guard.Text(terms.PurposeService, 300, "Purpose / service"), PurposeService, StringComparison.Ordinal)
      || (MoneyDirection is not null && terms.MoneyDirection != MoneyDirection);

    if (termsChanged)
      throw new DomainException(
        $"Contract {ContractNo.Value} is in force, so its terms are history: renew or revise it to change the amount, dates or service. Only the reference no. and the remarks can be corrected.");

    if (MoneyDirection is null && terms.MoneyDirection is { } direction)
      MoneyDirection = EnsureDirection(direction);

    ReferenceNo = Guard.Text(terms.ReferenceNo, 100, "Reference no.");
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }

  /// A draft comes into force.
  public void Activate(ContractStatus currentStatus, ContractStatus activeStatus)
  {
    EnsureCurrent(currentStatus);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Only a draft contract can be activated; {ContractNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}.");

    ContractStatusId = activeStatus.Id;
  }

  /// New terms, or the same contractor again after the end: a new contract row pointing back at this one; an active one
  /// becomes RENEWED and ends the day before the renewal starts, an expired one keeps its status.
  public PropertyOutsourcing Renew(
      ContractStatus currentStatus,
      ContractStatus renewedStatus,
      ContractStatus activeStatus,
      OutsourcingId newId,
      BusinessCode newContractNo,
      PropertyOwner contractor,
      Terms newTerms)
  {
    EnsureCurrent(currentStatus);
    ArgumentNullException.ThrowIfNull(contractor);
    ArgumentNullException.ThrowIfNull(newTerms);
    renewedStatus.EnsureIs(SystemMasterCodes.Renewed);
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    if (!currentStatus.Is(SystemMasterCodes.Active) && !currentStatus.Is(SystemMasterCodes.Expired))
      throw new DomainException($"Contract {ContractNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}; only an active or expired contract can be renewed.");

    if (newTerms.ContractStartDate <= ContractStartDate)
      throw new DomainException($"The renewal must start after {ContractNo.Value} started ({ContractStartDate:yyyy-MM-dd}).");

    contractor.EnsureActive();

    var renewal = new PropertyOutsourcing
    {
      Id = newId,
      PropertyId = PropertyId,
      OutsourcedPartyOwnerId = contractor.Id,
      ContractNo = newContractNo,
      ContractStatusId = activeStatus.Id,
      RenewedFromContractId = Id
    };

    renewal.Apply(newTerms with { MoneyDirection = newTerms.MoneyDirection ?? MoneyDirection });

    if (currentStatus.Is(SystemMasterCodes.Active))
    {
      ContractStatusId = renewedStatus.Id;

      // the two contracts never overlap: this one ends the day before the renewal starts
      var lastDay = newTerms.ContractStartDate.AddDays(-1);
      if (ContractEndDate is null || ContractEndDate > lastDay)
        ContractEndDate = lastDay;
    }

    return renewal;
  }

  /// The status act: only EXPIRED (the other statuses come from their own acts).
  public void ChangeStatus(ContractStatus currentStatus, ContractStatus status, DateOnly today)
  {
    ArgumentNullException.ThrowIfNull(status);

    if (status.Is(SystemMasterCodes.Terminated))
      throw new DomainException("Use terminate, so the date and reason are recorded.");

    if (!status.Is(SystemMasterCodes.Expired))
      throw new DomainException("A contract changes status only through its acts: activate, renew, terminate or mark expired.");

    Expire(currentStatus, status, today);
  }

  /// Marked once its end date has passed. An open-ended contract never expires (terminate it instead).
  public void Expire(ContractStatus currentStatus, ContractStatus expiredStatus, DateOnly today)
  {
    EnsureInForce(currentStatus);
    expiredStatus.EnsureIs(SystemMasterCodes.Expired);

    if (ContractEndDate is not { } end)
      throw new DomainException($"{ContractNo.Value} has no end date, so it cannot expire. Terminate it instead, or renew it with an end date.");

    if (today <= end)
      throw new DomainException($"{ContractNo.Value} ends on {end:yyyy-MM-dd}; it can be marked expired after that.");

    ContractStatusId = expiredStatus.Id;
  }

  /// Ends the contract early by an order (TERMINATED): not in the future, not after its agreed end.
  public void Terminate(ContractStatus currentStatus, ContractStatus terminatedStatus, DateOnly terminationDate, string reason, DateOnly? today = null)
  {
    EnsureInForce(currentStatus);
    terminatedStatus.EnsureIs(SystemMasterCodes.Terminated);

    AgreementDates.EnsureEndedOn(ContractNo.Value, terminationDate, ContractStartDate, ContractEndDate, today);

    ContractStatusId = terminatedStatus.Id;
    TerminationDate = terminationDate;
    TerminationReason = Guard.RequiredText(reason, 4000, "Termination reason");
  }

  /// A draft that will not go ahead is withdrawn (WITHDRAWN, with the reason): it never came into force.
  public void WithdrawDraft(ContractStatus currentStatus, ContractStatus withdrawnStatus, string reason)
  {
    EnsureCurrent(currentStatus);
    withdrawnStatus.EnsureIs(SystemMasterCodes.Withdrawn);

    if (!currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Only a draft can be withdrawn; {ContractNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()}. Terminate it instead.");

    ContractStatusId = withdrawnStatus.Id;
    TerminationReason = Guard.RequiredText(reason, 4000, "Termination reason");
  }

  public bool IsInForce(ContractStatus currentStatus) =>
      currentStatus.Id == ContractStatusId && currentStatus.Is(SystemMasterCodes.Active);

  /// The period the contract occupies the property, for the overlap rule (contracts of different types may coexist).
  public AgreementSpan Span(string contractorName) =>
      new(AgreementSpanKind.Contract, ContractNo.Value, contractorName, ContractStartDate, TerminationDate ?? ContractEndDate, OutsourcingTypeId);

  private void EnsureInForce(ContractStatus currentStatus)
  {
    EnsureCurrent(currentStatus);

    if (currentStatus.Is(SystemMasterCodes.Draft))
      throw new DomainException($"Contract {ContractNo.Value} is still a draft; activate it first.");

    if (!currentStatus.Is(SystemMasterCodes.Active))
      throw new DomainException($"Contract {ContractNo.Value} is {currentStatus.Name.Value.ToLowerInvariant()} and no longer in force.");
  }

  private void EnsureCurrent(ContractStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != ContractStatusId)
      throw new DomainException("The supplied status does not match the contract.");
  }

  private static MoneyDirection EnsureDirection(MoneyDirection direction) =>
      Enum.IsDefined(direction) ? direction : throw new DomainException("Unknown money direction.");

  private void Apply(Terms terms)
  {
    ArgumentNullException.ThrowIfNull(terms);
    ArgumentNullException.ThrowIfNull(terms.OutsourcingType);

    if (terms.OutsourcingType.Id != OutsourcingTypeId)
      terms.OutsourcingType.EnsureActive();

    if (terms.AmountFrequency is { } frequency && !Enum.IsDefined(frequency))
      throw new DomainException("Unknown amount frequency.");

    Guard.DateOrder(terms.ContractStartDate, terms.ContractEndDate, "Contract start date", "Contract end date");

    OutsourcingTypeId = terms.OutsourcingType.Id;
    ContractStartDate = terms.ContractStartDate;
    ContractEndDate = terms.ContractEndDate;
    PurposeService = Guard.Text(terms.PurposeService, 300, "Purpose / service");
    ContractAmount = Guard.NotNegative(terms.ContractAmount, "Contract amount");
    AmountFrequency = terms.AmountFrequency;
    MoneyDirection = terms.MoneyDirection is { } direction ? EnsureDirection(direction) : MoneyDirection;
    PerformanceGuarantee = Guard.NotNegative(terms.PerformanceGuarantee, "Performance guarantee");
    ReferenceNo = Guard.Text(terms.ReferenceNo, 100, "Reference no.");
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }
}
