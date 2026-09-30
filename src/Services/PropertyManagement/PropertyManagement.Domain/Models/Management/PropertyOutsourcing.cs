/// One contract handing the property (or a service on it) to a contractor.
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
  public decimal? PerformanceGuarantee { get; private set; }
  public string? ReferenceNo { get; private set; }
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
      string? Remarks);

  public static PropertyOutsourcing Create(OutsourcingId id, Property property, PropertyOwner contractor, BusinessCode contractNo, ContractStatus activeStatus, Terms terms)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(contractor);
    ArgumentNullException.ThrowIfNull(contractNo);
    ArgumentNullException.ThrowIfNull(activeStatus);
    property.EnsureActive();
    contractor.EnsureActive();
    activeStatus.EnsureIs(SystemMasterCodes.Active);

    var contract = new PropertyOutsourcing
    {
      Id = id,
      PropertyId = property.Id,
      OutsourcedPartyOwnerId = contractor.Id,
      ContractNo = contractNo,
      ContractStatusId = activeStatus.Id
    };

    contract.Apply(terms);
    return contract;
  }

  public void Update(ContractStatus currentStatus, Terms terms)
  {
    EnsureInForce(currentStatus);
    Apply(terms);
  }

  /// Expired or any other admin-defined status. Termination has its own step.
  public void ChangeStatus(ContractStatus currentStatus, ContractStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureInForce(currentStatus);
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Terminated))
      throw new DomainException("Use terminate, so the date and reason are recorded.");

    ContractStatusId = status.Id;
  }

  public void Terminate(ContractStatus currentStatus, ContractStatus terminatedStatus, DateOnly terminationDate, string reason)
  {
    EnsureInForce(currentStatus);
    terminatedStatus.EnsureIs(SystemMasterCodes.Terminated);

    if (terminationDate < ContractStartDate)
      throw new DomainException("A contract cannot end before it started.");

    ContractStatusId = terminatedStatus.Id;
    TerminationDate = terminationDate;
    TerminationReason = Guard.RequiredText(reason, 4000, "Termination reason");
  }

  private void EnsureInForce(ContractStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != ContractStatusId)
      throw new DomainException("The supplied status does not match the contract.");

    if (!currentStatus.Is(SystemMasterCodes.Active))
      throw new DomainException($"Contract {ContractNo.Value} is {currentStatus.Name.Value} and no longer in force.");
  }

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
    PerformanceGuarantee = Guard.NotNegative(terms.PerformanceGuarantee, "Performance guarantee");
    ReferenceNo = Guard.Text(terms.ReferenceNo, 100, "Reference no.");
    Remarks = Guard.Text(terms.Remarks, 4000, "Remarks");
  }
}
