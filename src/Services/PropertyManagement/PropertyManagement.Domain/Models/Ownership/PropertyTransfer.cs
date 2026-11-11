/// One transfer event — sale, gift, inheritance, exchange ... — with its parties.
///
/// Lifecycle: Initiated → (approve) Completed, or Initiated → Rejected / Cancelled. Approving a transfer IS the
/// mutation: in the same step every party's current ownership row is closed and a new row with the resulting
/// share is opened, pointing back at this transfer (schema guide, "Owners and ownership"), so the register never
/// shows an approved transfer whose old owners are still in place. Approved is kept only for rows approved
/// before that change; they are completed with Complete.
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
  /// The approving officer's name as it stood when they approved (read from the sign-in, never typed).
  public string? ApprovedBy { get; private set; }
  /// The approver's Identity user id and HRM employee id (when their account is linked to one).
  public Guid? ApprovedByUserId { get; private set; }
  public Guid? ApprovedByEmployeeId { get; private set; }
  public DateOnly? ApprovalDate { get; private set; }
  /// The approving order / sanction no.
  public string? ApprovalOrderRef { get; private set; }
  /// The Taxation no-dues certificate (clearance) the approval relied on, when one was given.
  public string? NoDuesCertificateNo { get; private set; }
  /// The day the ownership register was changed (server date) and the signed-in user who did it.
  public DateOnly? CompletedOn { get; private set; }
  public string? CompletedBy { get; private set; }
  /// Rejected or cancelled: the day, the signed-in user and the reason.
  public DateOnly? ClosedOn { get; private set; }
  public string? ClosedBy { get; private set; }
  public string? ClosingReason { get; private set; }
  public TransferStatus TransferStatus { get; private set; }
  /// How well TransferDate is known (old records).
  public DatePrecision TransferDatePrecision { get; private set; }
  /// Registered from an ownership history (HIS-…): only such transfers are corrected afterwards.
  public HistoryEntryId? FromHistoryEntryId { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<PropertyTransferParty> Parties => _parties.AsReadOnly();

  /// A transfer dated further ahead than this is probably a typing slip (2033 for 2023): it needs a confirmation.
  public const int FarFutureDays = 90;
  public const int NoDuesCertificateMaxLength = 50;

  /// Waiting for a decision: still holds the givers' shares.
  public bool IsPending => TransferStatus is TransferStatus.Initiated or TransferStatus.Approved;

  public sealed record PartyInput(OwnerId OwnerId, TransferPartyRole Role, decimal SharePct);

  /// Starts a transfer. Nothing changes hands until it is approved. The transfer may be dated in the future
  /// (it can then be approved from that date) - more than FarFutureDays ahead only when confirmed, since such a date
  /// blocks the givers' shares until then; every other rule is checked now so a waiting transfer is one that can be
  /// approved as it stands.
  public static PropertyTransfer Initiate(
      TransferId id,
      BusinessCode transferNo,
      TransferType transferType,
      DateOnly transferDate,
      string? transferReferenceNo,
      decimal? considerationAmount,
      string? relationship,
      string? remarks,
      IReadOnlyCollection<PartyInput> parties,
      OwnershipRecord record,
      bool confirmFarFuture = false)
  {
    ArgumentNullException.ThrowIfNull(transferNo);
    ArgumentNullException.ThrowIfNull(transferType);
    ArgumentNullException.ThrowIfNull(parties);
    ArgumentNullException.ThrowIfNull(record);
    record.Property.EnsureActive();
    transferType.EnsureActive();

    if (transferDate > record.Today.AddDays(FarFutureDays) && !confirmFarFuture)
      throw new DomainException(
        $"The transfer is dated {transferDate:yyyy-MM-dd}, more than {FarFutureDays} days ahead. Until then, the shares it gives cannot be used in another transfer. Check the date, or confirm that it is right.");

    relationship = Guard.Text(relationship, 100, "Relationship");
    if (transferType.RequiresRelationship && relationship is null)
      throw new DomainException($"A {transferType.Name.Value} transfer must record the relationship between the parties (e.g. Father -> Son).");

    var transfer = new PropertyTransfer
    {
      Id = id,
      PropertyId = record.Property.Id,
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

    transfer.Validate(record, atApproval: false);
    transfer.EnsureDeadGiversPassOn(transferType, record);
    transfer.ShareTransferredPct = transfer.Given().Values.Sum();

    return transfer;
  }

  /// Enters a transfer that already happened (e.g. from the paper register) as COMPLETED in one step, under the
  /// same rules as initiate + approve. It cannot be dated in the future. Used when the PROPERTY_TRANSFER separation rule
  /// is off; while it is on, RecordForApproval keeps the entry waiting for a second officer.
  public static (PropertyTransfer Transfer, OwnershipChange Change) RecordCompleted(
      TransferId id,
      BusinessCode transferNo,
      TransferType transferType,
      DateOnly transferDate,
      string? transferReferenceNo,
      decimal? considerationAmount,
      string? relationship,
      string? remarks,
      IReadOnlyCollection<PartyInput> parties,
      Approver approver,
      DateOnly? approvalDate,
      string? approvalOrderRef,
      string? noDuesCertificateNo,
      string completedBy,
      OwnershipRecord record,
      TenureType newTenureType,
      DatePrecision precision = DatePrecision.Exact)
  {
    ArgumentNullException.ThrowIfNull(record);
    EnsurePast(transferDate, record.Today);

    var transfer = Initiate(id, transferNo, transferType, transferDate, transferReferenceNo, considerationAmount, relationship, remarks, parties, record);
    transfer.TransferDatePrecision = precision;
    var change = transfer.Approve(approver, approvalDate ?? transferDate, approvalOrderRef, noDuesCertificateNo, completedBy, record, newTenureType, transferType);
    return (transfer, change);
  }

  /// A transfer that already happened, entered while two people must pass every transfer (the PROPERTY_TRANSFER rule):
  /// it is saved waiting for approval and nothing changes hands. The approval the clerk read off the paper - its date
  /// (default the transfer date), order and no-dues certificate - is kept on it, so the second officer checks the entry
  /// against the paper and approves it as it stands (IsRecordedPast).
  public static PropertyTransfer RecordForApproval(
      TransferId id,
      BusinessCode transferNo,
      TransferType transferType,
      DateOnly transferDate,
      string? transferReferenceNo,
      decimal? considerationAmount,
      string? relationship,
      string? remarks,
      IReadOnlyCollection<PartyInput> parties,
      DateOnly? approvalDate,
      string? approvalOrderRef,
      string? noDuesCertificateNo,
      OwnershipRecord record)
  {
    ArgumentNullException.ThrowIfNull(record);
    EnsurePast(transferDate, record.Today);

    var date = approvalDate ?? transferDate;
    if (date > record.Today)
      throw new DomainException($"The approval date cannot be in the future ({date:yyyy-MM-dd}). Today is {record.Today:yyyy-MM-dd}.");
    if (date < transferDate)
      throw new DomainException($"A transfer cannot be approved before its transfer date ({transferDate:yyyy-MM-dd}).");

    var transfer = Initiate(id, transferNo, transferType, transferDate, transferReferenceNo, considerationAmount, relationship, remarks, parties, record);
    transfer.ApprovalDate = date;
    transfer.ApprovalOrderRef = Guard.Text(approvalOrderRef, 100, "Approval order ref");
    transfer.NoDuesCertificateNo = Guard.Text(noDuesCertificateNo, NoDuesCertificateMaxLength, "No-dues certificate no.");
    return transfer;
  }

  /// Entered after the fact and waiting for a second officer (RecordForApproval): the approval date, order and
  /// certificate on it are the ones read off the paper, not yet anyone's approval here.
  public bool IsRecordedPast => TransferStatus == TransferStatus.Initiated && ApprovalDate is not null;

  private static void EnsurePast(DateOnly transferDate, DateOnly today)
  {
    if (transferDate > today)
      throw new DomainException(
        $"A transfer that already happened cannot be dated in the future ({transferDate:yyyy-MM-dd}). Today is {today:yyyy-MM-dd}. Initiate it instead.");
  }

  /// Approves AND completes the transfer in one step: the rules are checked again against the record as it
  /// stands now, then the givers' rows are closed and the resulting rows opened. The approver is the signed-in
  /// officer. Whether the person who started it may approve it is the PROPERTY_TRANSFER separation-of-duties rule,
  /// which the handler checks before calling this (Administration switches it, so it is not a fixed domain rule).
  public OwnershipChange Approve(
      Approver approver,
      DateOnly approvalDate,
      string? approvalOrderRef,
      string? noDuesCertificateNo,
      string completedBy,
      OwnershipRecord record,
      TenureType newTenureType,
      TransferType transferType)
  {
    ArgumentNullException.ThrowIfNull(approver);
    ArgumentNullException.ThrowIfNull(record);

    switch (TransferStatus)
    {
      case TransferStatus.Initiated:
        break;
      case TransferStatus.Approved:
        throw new DomainException($"{TransferNo.Value} was approved earlier but not completed. Complete it to register the change.");
      default:
        throw new DomainException($"{TransferNo.Value} is {Describe(TransferStatus)} and can no longer be approved.");
    }

    EnsureDue(record.Today);

    if (approvalDate > record.Today)
      throw new DomainException($"The approval date cannot be in the future ({approvalDate:yyyy-MM-dd}). Today is {record.Today:yyyy-MM-dd}.");

    if (approvalDate < TransferDate)
      throw new DomainException($"A transfer cannot be approved before its transfer date ({TransferDate:yyyy-MM-dd}).");

    var approverName = Guard.RequiredText(approver.Name, Approver.NameMaxLength, "Approved by");
    var orderRef = Guard.Text(approvalOrderRef, 100, "Approval order ref");
    var certificate = Guard.Text(noDuesCertificateNo, NoDuesCertificateMaxLength, "No-dues certificate no.");

    EnsureDeadGiversPassOn(transferType, record);
    var change = Apply(record, newTenureType, completedBy);

    ApprovedBy = approverName;
    ApprovedByUserId = approver.UserId;
    ApprovedByEmployeeId = approver.EmployeeId;
    ApprovalDate = approvalDate;
    ApprovalOrderRef = orderRef;
    NoDuesCertificateNo = certificate;
    return change;
  }

  /// Registers a transfer approved before approval and completion became one step (status APPROVED).
  public OwnershipChange Complete(string completedBy, string? approvalOrderRef, OwnershipRecord record, TenureType newTenureType, TransferType transferType)
  {
    ArgumentNullException.ThrowIfNull(record);

    switch (TransferStatus)
    {
      case TransferStatus.Approved:
        break;
      case TransferStatus.Initiated:
        throw new DomainException($"{TransferNo.Value} is waiting for approval. Approve it to register the change.");
      default:
        throw new DomainException($"{TransferNo.Value} is {Describe(TransferStatus)} and cannot be completed.");
    }

    EnsureDue(record.Today);

    var orderRef = Guard.Text(approvalOrderRef, 100, "Approval order ref");
    EnsureDeadGiversPassOn(transferType, record);
    var change = Apply(record, newTenureType, completedBy);

    ApprovalOrderRef = orderRef ?? ApprovalOrderRef;
    return change;
  }

  /// The approving officer refuses the transfer. Nothing changes hands.
  public void Reject(Property property, string reason, string rejectedBy, DateOnly today)
  {
    EnsureOn(property);

    if (TransferStatus != TransferStatus.Initiated)
      throw new DomainException($"Only a transfer waiting for approval can be rejected. {TransferNo.Value} is {Describe(TransferStatus)}.");

    Close(TransferStatus.Rejected, reason, "Rejection reason", rejectedBy, today);
  }

  /// Withdraws a transfer that has not changed ownership yet. A completed transfer is history: reverse it
  /// with a new transfer.
  public void Cancel(Property property, string reason, string cancelledBy, DateOnly today)
  {
    EnsureOn(property);

    if (TransferStatus == TransferStatus.Completed)
      throw new DomainException($"{TransferNo.Value} is completed and cannot be cancelled. To reverse it, record a new transfer.");

    if (!IsPending)
      throw new DomainException($"{TransferNo.Value} is already {Describe(TransferStatus)}.");

    Close(TransferStatus.Cancelled, reason, "Cancellation reason", cancelledBy, today);
  }

  public sealed record OwnershipChange(IReadOnlyList<PropertyOwnership> Closed, IReadOnlyList<PropertyOwnership> Opened);

  /// Applies the transfer to the property's current ownership.
  ///
  /// For every party the net change is worked out (received − given):
  /// - a net giver's rows are closed on the transfer date; if they keep part of the share, a remainder row is
  ///   opened that keeps the closed row's acquisition details ("Remainder after TRF-…");
  /// - a net receiver's free rows (active and not charged) are closed and one row with the resulting share is
  ///   opened, pointing at this transfer. A disputed or charged row of a receiver is left as it is, so the
  ///   dispute or the charge stays on the share it concerns.
  /// Parties who owned nothing before get newTenureType; existing owners keep their tenure.
  private OwnershipChange Apply(OwnershipRecord record, TenureType newTenureType, string completedBy)
  {
    ArgumentNullException.ThrowIfNull(newTenureType);

    Validate(record, atApproval: true);
    var completer = Guard.RequiredText(completedBy, 150, "Completed by");

    var given = Given();
    var received = Received();

    // a newcomer gets the default tenure: check it before any row is touched
    if (received.Keys.Any(id => !record.CurrentRowsOf(id).Any()))
      newTenureType.EnsureActive();

    var closed = new List<PropertyOwnership>();
    var opened = new List<PropertyOwnership>();

    foreach (var ownerId in given.Keys.Union(received.Keys))
    {
      var delta = received.GetValueOrDefault(ownerId) - given.GetValueOrDefault(ownerId);
      if (delta == 0)
        continue;

      var current = record.CurrentRowsOf(ownerId).ToList();
      var rows = delta < 0
        ? current
        : current.Where(r => r.IsTransferable && record.ChargeOn(r) is null).ToList();
      var held = rows.Sum(r => r.OwnershipSharePct);
      var newShare = held + delta;
      var basis = rows.OrderBy(r => r.EffectiveFrom).FirstOrDefault();

      foreach (var row in rows)
      {
        row.End(TransferDate, $"Closed by transfer {TransferNo.Value}.");
        row.ClosedBy(Id);
        closed.Add(row);
      }

      if (newShare <= 0)
        continue;

      if (delta < 0 && basis is not null)
      {
        opened.Add(PropertyOwnership.New(
          OwnershipId.New(), PropertyId, ownerId, basis.TenureTypeId, newShare, TransferDate,
          basis.AcquisitionTransferTypeId, basis.AcquiredViaTransferId, basis.AcquiredViaAllotmentId, basis.ReferenceNo,
          remarks: $"Remainder after {TransferNo.Value}."));
        continue;
      }

      var tenureId = basis?.TenureTypeId ?? current.FirstOrDefault()?.TenureTypeId;

      opened.Add(PropertyOwnership.New(
        OwnershipId.New(), PropertyId, ownerId, tenureId ?? newTenureType.Id, newShare, TransferDate,
        TransferTypeId, Id, acquiredViaAllotmentId: null, TransferReferenceNo,
        remarks: held > 0
          ? $"Created by transfer {TransferNo.Value}: {held:0.####}% already owned + {delta:0.####}% received."
          : $"Created by transfer {TransferNo.Value}."));
    }

    foreach (var row in opened)
      row.OpenedBy(Id, TransferDatePrecision);

    SettleEnforcedCharges(record, closed, opened, received.Keys.ToList());

    TransferStatus = TransferStatus.Completed;
    CompletedOn = record.Today;
    CompletedBy = completer;
    return new OwnershipChange(closed, opened);
  }

  /// An enforced charge is settled once its share has gone to the charge holder: a charge on one share when that
  /// row was closed by this transfer to the holder, a charge on the whole property when the holder now holds
  /// every share that is still held.
  private void SettleEnforcedCharges(OwnershipRecord record, List<PropertyOwnership> closed, List<PropertyOwnership> opened, IReadOnlyCollection<OwnerId> receivers)
  {
    var closedIds = closed.Select(r => r.Id).ToHashSet();
    var heldAfter = record.Current.Where(r => !closedIds.Contains(r.Id)).Concat(opened).ToList();

    foreach (var charge in record.Charges.Where(c => c.Status == EncumbranceStatus.Enforced && c.AllowsTransferTo(receivers)))
    {
      var settled = charge.OwnershipId is { } chargedRow
        ? closedIds.Contains(chargedRow)
        : heldAfter.All(r => r.OwnerId == charge.HolderOwnerId);

      if (settled)
        charge.Satisfy(TransferNo.Value, TransferDate);
    }
  }

  private Dictionary<OwnerId, decimal> Given() => Side(TransferPartyRole.Transferor);

  private Dictionary<OwnerId, decimal> Received() => Side(TransferPartyRole.Transferee);

  private Dictionary<OwnerId, decimal> Side(TransferPartyRole role) =>
      _parties.Where(p => p.PartyRole == role).GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Sum(p => p.SharePct));

  /// Every rule a transfer must meet, at initiation and again at approval (the record may have changed since).
  private void Validate(OwnershipRecord record, bool atApproval)
  {
    ArgumentNullException.ThrowIfNull(record);

    if (record.Property.Id != PropertyId)
      throw new DomainException("The ownership record belongs to another property.");

    record.Property.EnsureActive();
    var code = record.Property.PropertyCode.Value;

    if (_parties.All(p => p.PartyRole != TransferPartyRole.Transferor) || _parties.All(p => p.PartyRole != TransferPartyRole.Transferee))
      throw new DomainException("A transfer needs at least one transferor and one transferee.");

    if (_parties.GroupBy(p => (p.OwnerId, p.PartyRole)).Any(g => g.Count() > 1))
      throw new DomainException("An owner can appear only once on each side of a transfer.");

    var given = Given();
    var received = Received();

    var totalGiven = given.Values.Sum();
    var totalReceived = received.Values.Sum();
    if (totalGiven != totalReceived)
      throw new DomainException(
        $"Transferors give {totalGiven:0.####}% but transferees receive {totalReceived:0.####}%. The two sides must match.");

    var changing = given.Keys.Union(received.Keys)
        .Where(id => received.GetValueOrDefault(id) - given.GetValueOrDefault(id) != 0)
        .ToList();

    if (changing.Count == 0)
      throw new DomainException("This transfer moves no share.");

    // every party is a registered, active owner record
    foreach (var ownerId in given.Keys.Union(received.Keys))
    {
      var owner = record.Owner(ownerId)
        ?? throw new DomainException("A party of this transfer is not a registered owner.");

      if (!owner.IsActive)
        throw new DomainException($"{owner.OwnerName.Value} ({owner.OwnerCode.Value}) is inactive. Reactivate the owner record before using it in a transfer.");

      if (received.GetValueOrDefault(ownerId) - given.GetValueOrDefault(ownerId) > 0)
      {
        owner.EnsureAliveOn(TransferDate);
        if (!record.FromOldRecords)
          owner.EnsureCanHoldToday();
      }
    }

    // a charge on the whole property stops every transfer (an enforced one: every transfer except to its holder)
    var receivers = received.Keys.ToList();
    if (record.PropertyCharge is { } propertyCharge && !propertyCharge.AllowsTransferTo(receivers))
      throw new DomainException(propertyCharge.Status == EncumbranceStatus.Enforced
        ? $"{code} is under {propertyCharge.Describe()} on the whole property. The charge was enforced, so shares can only be transferred to {propertyCharge.HolderName}."
        : $"{code} is under {propertyCharge.Describe()} on the whole property. Release the charge before any transfer.");

    foreach (var (ownerId, share) in given)
    {
      var name = record.NameOf(ownerId);
      var current = record.CurrentRowsOf(ownerId).ToList();

      if (current.Count == 0)
        throw new DomainException($"{name} is not a current owner of {code}. Only a current owner can give a share.");

      var disputed = current.Where(r => !r.IsTransferable).Sum(r => r.OwnershipSharePct);
      if (disputed > 0)
        throw new DomainException($"{name}'s {disputed:0.####}% is disputed. Resolve the dispute before transferring it.");

      var held = current.Sum(r => r.OwnershipSharePct);
      if (share > held)
        throw new DomainException(atApproval
          ? $"{name} now owns {held:0.####}% of {code}, not the {share:0.####}% this transfer gives. Cancel {TransferNo.Value} and initiate a new transfer."
          : $"{name} cannot give {share:0.####}% because they own only {held:0.####}%.");

      var others = record.PendingTransfersGivenBy(ownerId, except: Id).ToList();
      var committed = others.Sum(t => t._parties.Where(p => p.OwnerId == ownerId && p.PartyRole == TransferPartyRole.Transferor).Sum(p => p.SharePct));
      if (committed + share > held)
        throw new DomainException(
          $"{name} owns {held:0.####}%. Of this, {committed:0.####}% is already being transferred in {string.Join(", ", others.Select(t => t.TransferNo.Value))}. Cancel or approve that transfer first.");

      if (current.Select(record.ChargeOn).FirstOrDefault(c => c is not null && !c.AllowsTransferTo(receivers)) is { } charge)
        throw new DomainException(charge.Status == EncumbranceStatus.Enforced
          ? $"{name}'s share is under {charge.Describe()}. The charge was enforced, so the share can only be transferred to {charge.HolderName}."
          : $"{name}'s share is under {charge.Describe()}. Release the charge before the share is transferred.");
    }

    // the new rows start on the transfer date: it must come after every party's earlier entries
    foreach (var ownerId in changing)
      OwnershipHistory.EnsureCanStart(record.NameOf(ownerId), record.RowsOf(ownerId), TransferDate, "the transfer");
  }

  /// A transfer from the old records: how well its date is known, and the history entry it came from.
  internal void FromHistory(HistoryEntryId entry, DatePrecision precision)
  {
    FromHistoryEntryId = entry;
    TransferDatePrecision = precision;
  }

  /// A correction of a transfer from the old records (date, kind, deed): the rows it opened and closed move with it.
  public void CorrectHistory(TransferType transferType, DateOnly date, DatePrecision precision, string? referenceNo, string? relationship,
      OwnershipRecord record, DateOnly? earliest, DateOnly? latest)
  {
    ArgumentNullException.ThrowIfNull(transferType);
    ArgumentNullException.ThrowIfNull(record);
    record.Property.EnsureActive();
    if (FromHistoryEntryId is null || TransferStatus != TransferStatus.Completed)
      throw new DomainException($"{TransferNo.Value} was not entered from the old records. To correct a transfer, record another transfer.");
    transferType.EnsureActive();
    if (earliest is { } after && date <= after)
      throw new DomainException($"{TransferNo.Value} must stay after the step before it ({after:dd'/'MM'/'yyyy}).");
    if (latest is { } before && date >= before)
      throw new DomainException($"{TransferNo.Value} must stay before the step after it ({before:dd'/'MM'/'yyyy}).");
    if (date > record.Today)
      throw new DomainException("A transfer from the old records cannot be dated in the future.");

    relationship = Guard.Text(relationship, 100, "Relationship");
    if (transferType.RequiresRelationship && relationship is null)
      throw new DomainException($"A {transferType.Name.Value} transfer must record the relationship between the parties (e.g. Father -> Son).");

    TransferDate = date;
    TransferDatePrecision = precision;
    TransferTypeId = transferType.Id;
    TransferReferenceNo = Guard.Text(referenceNo, 100, "Transfer reference no.");
    Relationship = relationship;
    EnsureDeadGiversPassOn(transferType, record);
    foreach (var party in _parties.Where(p => p.PartyRole == TransferPartyRole.Transferee))
      record.Owner(party.OwnerId)?.EnsureAliveOn(date);

    foreach (var row in record.Ownerships.Where(r => r.OpenedByTransferId == Id))
      row.MoveStart(date, precision);
    foreach (var row in record.Ownerships.Where(r => r.ClosedByTransferId == Id))
      row.MoveEnd(date);
  }

  /// After their death an owner's share moves only by inheritance or a court's decree.
  private void EnsureDeadGiversPassOn(TransferType transferType, OwnershipRecord record)
  {
    if (transferType.Is(SystemMasterCodes.TransferInheritance) || transferType.Is(SystemMasterCodes.TransferCourtDecree))
      return;

    foreach (var ownerId in Given().Keys)
      if (record.Owner(ownerId) is { DateOfDeath: { } died } owner && TransferDate > died)
        throw new DomainException(
          $"{owner.OwnerName.Value} died on {died:dd'/'MM'/'yyyy}, before this transfer ({TransferDate:dd'/'MM'/'yyyy}). Their share can pass only by inheritance or a court decree.");
  }

  private void EnsureDue(DateOnly today)
  {
    if (TransferDate > today)
      throw new DomainException($"{TransferNo.Value} can be approved on or after {TransferDate:yyyy-MM-dd}.");
  }

  private void EnsureOn(Property property)
  {
    ArgumentNullException.ThrowIfNull(property);

    if (property.Id != PropertyId)
      throw new DomainException("The transfer belongs to another property.");

    property.EnsureActive();
  }

  private void Close(TransferStatus status, string reason, string reasonField, string closedBy, DateOnly today)
  {
    ClosingReason = Guard.RequiredText(reason, 4000, reasonField);
    ClosedBy = Guard.RequiredText(closedBy, 150, "Closed by");
    ClosedOn = today;
    TransferStatus = status;
  }

  private static string Describe(TransferStatus status) => status switch
  {
    TransferStatus.Initiated => "waiting for approval",
    TransferStatus.Approved => "approved but not completed",
    _ => status.ToString().ToLowerInvariant()
  };
}

/// One person on one side of a transfer. Supports multi-party transfers such as
/// Owner B + Owner C → Owner C + Owner D.
public class PropertyTransferParty : Entity<TransferPartyId>
{
  public TransferId PropertyTransferId { get; private set; } = default!;
  /// The transfer it belongs to (set by EF when both are loaded; the audit trail reads its property through it).
  public PropertyTransfer? Transfer { get; private set; }
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
