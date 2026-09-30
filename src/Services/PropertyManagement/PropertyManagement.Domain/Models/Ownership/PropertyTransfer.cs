/// One transfer event — sale, gift, inheritance, exchange ... — with its parties.
///
/// Lifecycle: Initiated → Approved → Completed (or Cancelled before completion). Completing it is what
/// changes ownership: every party's current ownership row is closed and a new row with the resulting
/// share is opened, pointing back at this transfer (schema guide, "Owners and ownership").
public class PropertyTransfer : Aggregate<TransferId>
{
  private readonly List<PropertyTransferParty> _parties = new();

  public PropertyId PropertyId { get; private set; } = default!;
  /// TRF-00001, issued by the TRANSFER code sequence.
  public BusinessCode TransferNo { get; private set; } = default!;
  public MasterId TransferTypeId { get; private set; } = default!;
  public DateOnly TransferDate { get; private set; }
  /// Mutation / registry / order no.
  public string? TransferReferenceNo { get; private set; }
  /// Portion of the property moved: the transferors' shares added up.
  public decimal ShareTransferredPct { get; private set; }
  /// Sale price / exchange value — informational; tax is the Tax Module's job.
  public decimal? ConsiderationAmount { get; private set; }
  /// Father → Son ... required when the transfer type asks for it (Gift, Inheritance).
  public string? Relationship { get; private set; }
  public string? ApprovedBy { get; private set; }
  public DateOnly? ApprovalDate { get; private set; }
  public TransferStatus TransferStatus { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<PropertyTransferParty> Parties => _parties.AsReadOnly();

  public sealed record PartyInput(OwnerId OwnerId, TransferPartyRole Role, decimal SharePct);

  public static PropertyTransfer Initiate(
      TransferId id,
      Property property,
      BusinessCode transferNo,
      TransferType transferType,
      DateOnly transferDate,
      string? transferReferenceNo,
      decimal? considerationAmount,
      string? relationship,
      string? remarks,
      IReadOnlyCollection<PartyInput> parties,
      IReadOnlyCollection<PropertyOwnership> currentOwnerships)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(transferNo);
    ArgumentNullException.ThrowIfNull(transferType);
    ArgumentNullException.ThrowIfNull(parties);
    property.EnsureActive();
    transferType.EnsureActive();

    relationship = Guard.Text(relationship, 100, "Relationship");
    if (transferType.RequiresRelationship && relationship is null)
      throw new DomainException($"A {transferType.Name.Value} transfer must record the relationship between the parties (e.g. Father -> Son).");

    var transfer = new PropertyTransfer
    {
      Id = id,
      PropertyId = property.Id,
      TransferNo = transferNo,
      TransferTypeId = transferType.Id,
      TransferDate = transferDate,
      TransferReferenceNo = Guard.Text(transferReferenceNo, 100, "Transfer reference no."),
      ConsiderationAmount = Guard.NotNegative(considerationAmount, "Consideration amount"),
      Relationship = relationship,
      TransferStatus = TransferStatus.Initiated,
      Remarks = Guard.Text(remarks, 4000, "Remarks")
    };

    foreach (var party in parties)
      transfer._parties.Add(PropertyTransferParty.Create(TransferPartyId.New(), id, party.OwnerId, party.Role, party.SharePct));

    transfer.ValidateParties(currentOwnerships);
    transfer.ShareTransferredPct = transfer.Given().Values.Sum();

    return transfer;
  }

  public void Approve(string approvedBy, DateOnly approvalDate)
  {
    if (TransferStatus != TransferStatus.Initiated)
      throw new DomainException($"Only an initiated transfer can be approved; this one is {TransferStatus}.");

    if (approvalDate < TransferDate)
      throw new DomainException("A transfer cannot be approved before its transfer date.");

    ApprovedBy = Guard.RequiredText(approvedBy, 150, "Approved by");
    ApprovalDate = approvalDate;
    TransferStatus = TransferStatus.Approved;
  }

  public void Cancel(string? reason)
  {
    if (TransferStatus is TransferStatus.Completed or TransferStatus.Cancelled)
      throw new DomainException($"A {TransferStatus.ToString().ToLowerInvariant()} transfer cannot be cancelled.");

    TransferStatus = TransferStatus.Cancelled;
    Remarks = Guard.Text(reason, 4000, "Reason") ?? Remarks;
  }

  public sealed record OwnershipChange(IReadOnlyList<PropertyOwnership> Closed, IReadOnlyList<PropertyOwnership> Opened);

  /// Applies the transfer to the property's current ownership.
  ///
  /// For every party the net change is worked out (received − given). Their current row(s) are closed on
  /// the transfer date and, if they still hold something, one new row with the resulting share is opened.
  /// Parties who owned nothing before get newTenureType; existing owners keep their tenure.
  public OwnershipChange Complete(IReadOnlyCollection<PropertyOwnership> currentOwnerships, TenureType newTenureType)
  {
    ArgumentNullException.ThrowIfNull(currentOwnerships);
    ArgumentNullException.ThrowIfNull(newTenureType);

    if (TransferStatus != TransferStatus.Approved)
      throw new DomainException($"Only an approved transfer can be completed; this one is {TransferStatus}.");

    // ownership may have changed since the transfer was initiated
    ValidateParties(currentOwnerships);

    var given = Given();
    var received = _parties.Where(p => p.PartyRole == TransferPartyRole.Transferee)
        .GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Sum(p => p.SharePct));

    var closed = new List<PropertyOwnership>();
    var opened = new List<PropertyOwnership>();

    foreach (var ownerId in given.Keys.Union(received.Keys))
    {
      var delta = received.GetValueOrDefault(ownerId) - given.GetValueOrDefault(ownerId);
      if (delta == 0)
        continue;

      var rows = currentOwnerships.Where(o => o.OwnerId == ownerId && o.IsCurrent).ToList();
      var newShare = rows.Sum(r => r.OwnershipSharePct) + delta;

      if (rows.Any(r => r.EffectiveFrom == TransferDate))
        throw new DomainException(
          $"An ownership row for this owner already starts on {TransferDate:yyyy-MM-dd}; record the transfer on a later date.");

      if (!newTenureType.IsActive && rows.Count == 0)
        newTenureType.EnsureActive();

      foreach (var row in rows)
      {
        row.End(TransferDate, $"Closed by transfer {TransferNo.Value}.");
        closed.Add(row);
      }

      if (newShare > 0)
        opened.Add(PropertyOwnership.New(
          OwnershipId.New(), PropertyId, ownerId,
          rows.FirstOrDefault()?.TenureTypeId ?? newTenureType.Id,
          newShare, TransferDate, TransferTypeId, Id, TransferReferenceNo,
          remarks: $"Created by transfer {TransferNo.Value}."));
    }

    TransferStatus = TransferStatus.Completed;
    return new OwnershipChange(closed, opened);
  }

  private Dictionary<OwnerId, decimal> Given() =>
      _parties.Where(p => p.PartyRole == TransferPartyRole.Transferor)
        .GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Sum(p => p.SharePct));

  private void ValidateParties(IReadOnlyCollection<PropertyOwnership> currentOwnerships)
  {
    ArgumentNullException.ThrowIfNull(currentOwnerships);

    if (_parties.All(p => p.PartyRole != TransferPartyRole.Transferor) || _parties.All(p => p.PartyRole != TransferPartyRole.Transferee))
      throw new DomainException("A transfer needs at least one transferor and one transferee.");

    if (_parties.GroupBy(p => (p.OwnerId, p.PartyRole)).Any(g => g.Count() > 1))
      throw new DomainException("An owner can appear only once on each side of a transfer.");

    var given = Given();

    foreach (var (ownerId, share) in given)
    {
      var held = currentOwnerships.Where(o => o.PropertyId == PropertyId && o.OwnerId == ownerId && o.IsCurrent)
          .Sum(o => o.OwnershipSharePct);

      if (held == 0)
        throw new DomainException("Every transferor must be a current owner of the property.");

      if (share > held)
        throw new DomainException($"A transferor cannot give {share:0.####}% while holding {held:0.####}%.");
    }

    var totalGiven = given.Values.Sum();
    var totalReceived = _parties.Where(p => p.PartyRole == TransferPartyRole.Transferee).Sum(p => p.SharePct);

    if (totalGiven != totalReceived)
      throw new DomainException(
        $"Transferors give {totalGiven:0.####}% but transferees receive {totalReceived:0.####}%; the two sides must match.");
  }
}

/// One person on one side of a transfer. Supports multi-party transfers such as
/// Owner B + Owner C → Owner C + Owner D.
public class PropertyTransferParty : Entity<TransferPartyId>
{
  public TransferId PropertyTransferId { get; private set; } = default!;
  public OwnerId OwnerId { get; private set; } = default!;
  public TransferPartyRole PartyRole { get; private set; }
  /// Share given (transferor) or received (transferee).
  public decimal SharePct { get; private set; }

  internal static PropertyTransferParty Create(TransferPartyId id, TransferId transferId, OwnerId ownerId, TransferPartyRole role, decimal sharePct)
  {
    ArgumentNullException.ThrowIfNull(ownerId);

    if (!Enum.IsDefined(role))
      throw new DomainException("Unknown transfer party role.");

    return new PropertyTransferParty
    {
      Id = id,
      PropertyTransferId = transferId,
      OwnerId = ownerId,
      PartyRole = role,
      SharePct = Guard.SharePct(sharePct, "Party share")
    };
  }
}
