/// taxation.psid_sync: the amount behind a property's constant 1Link reference after a change, one row per change
/// (annual demand, a surcharge "add up", a payment, a correction). No 1Link connector exists yet, so every row stays
/// PENDING until one acknowledges it.
public class PsidSync : Aggregate<PsidSyncId>
{
  public TaxFileId TaxFileId { get; private set; } = default!;
  public string Psid { get; private set; } = default!;
  public int SyncNo { get; private set; }
  public PsidSyncReason Reason { get; private set; }
  public DemandId? DemandId { get; private set; }
  public PenaltyId? PenaltyId { get; private set; }
  public decimal AmountBefore { get; private set; }
  public decimal AmountAfter { get; private set; }
  public DateOnly? DueDate { get; private set; }
  public DateTime SyncedAt { get; private set; }
  public string? OnelinkAck { get; private set; }
  public string? Note { get; private set; }
  public PsidSyncStatus Status { get; private set; }

  private PsidSync() { }

  public static PsidSync Record(PsidSyncId id, TaxFile file, PsidSync? previous, PsidSyncReason reason, decimal amountAfter, DemandId? demandId, PenaltyId? penaltyId,
      DateOnly? dueDate, string? note, DateTime now) => new()
  {
    Id = id,
    TaxFileId = file.Id,
    Psid = file.Psid ?? throw new DomainException($"{file.FileNo} has no 1Link reference."),
    SyncNo = (previous?.SyncNo ?? 0) + 1,
    Reason = reason,
    DemandId = demandId,
    PenaltyId = penaltyId,
    AmountBefore = previous?.AmountAfter ?? 0m,
    AmountAfter = Rupees.Round(Math.Max(0, amountAfter)),
    DueDate = dueDate,
    SyncedAt = now,
    Note = note is null ? null : note.Length <= 300 ? note : note[..297] + "...",
    Status = PsidSyncStatus.Pending
  };

  public void Acknowledge(string ack)
  {
    OnelinkAck = Guard.RequiredText(ack, 60, "Acknowledgement");
    Status = PsidSyncStatus.Acked;
  }
}

/// taxation.payment: money received. Against a property's constant reference it settles oldest year first, item by item
/// (payment_allocation); a fee receipt pays one fee. 1Link, QR and online payments carry the bank's transaction
/// reference, which makes posting idempotent; cash and bank-counter payments carry the Section's manual receipt number
/// (unique). The payer's name is kept as it was on the day, so a receipt reprints the same. A reversed payment (a bounced
/// cheque, a wrong posting) keeps its rows; what it paid is owed again.
public class Payment : Aggregate<PaymentId>
{
  private readonly List<PaymentAllocation> _allocations = new();

  public TaxFileId? TaxFileId { get; private set; }
  public string? Psid { get; private set; }
  public PartyId? PartyId { get; private set; }
  public FeeId? FeeId { get; private set; }
  public string? PayerName { get; private set; }
  public Guid? ChallanId { get; private set; }
  public PaymentChannel Channel { get; private set; }
  public string? ReceiptNo { get; private set; }
  public Guid? ReceivedBy { get; private set; }
  public string? BankCode { get; private set; }
  public string? TransactionRef { get; private set; }
  public DateTime PaidAt { get; private set; }
  public decimal Amount { get; private set; }
  public DateTime? PostedAt { get; private set; }
  public DateTime? AccountsPostedAt { get; private set; }
  public PaymentStatus Status { get; private set; }
  public DateTime? ReversedAt { get; private set; }
  public Guid? ReversedBy { get; private set; }
  public string? ReversalReason { get; private set; }

  /// Concurrency token (Postgres xmin): a payment reversed twice at once is reversed once.
  public uint Version { get; private set; }

  public IReadOnlyList<PaymentAllocation> Allocations => _allocations.AsReadOnly();

  public static bool IsManual(PaymentChannel channel) => channel is PaymentChannel.Cash or PaymentChannel.BankCounter;

  public bool IsFeeReceipt => FeeId is not null;

  private Payment() { }

  /// Money against a file's reference (it settles the demands and the opening arrears behind it).
  public static Payment Receive(PaymentId id, TaxFile file, string? payerName, PaymentChannel channel, decimal amount, string? transactionRef, string? receiptNo,
      string? bankCode, DateTime paidAt, Guid? receivedBy, DateTime now)
  {
    if (file.Psid is null)
      throw new DomainException($"{file.FileNo} has no 1Link reference.");
    var payment = Create(id, channel, amount, transactionRef, receiptNo, bankCode, paidAt, receivedBy, now);
    payment.TaxFileId = file.Id;
    payment.Psid = file.Psid;
    payment.PayerName = Clip(payerName);
    return payment;
  }

  /// A fee paid at the counter (or by bank): one fee, its whole amount.
  public static Payment ForFee(PaymentId id, Fee fee, TaxFile? file, string payerName, PaymentChannel channel, string? transactionRef, string? receiptNo, string? bankCode,
      DateTime paidAt, Guid? receivedBy, DateTime now)
  {
    if (fee.Status != FeeStatus.Due)
      throw new DomainException($"Fee {fee.FeeNo} is {EnumText.Words(fee.Status)}; only a due fee is paid.");
    if (fee.Amount <= 0)
      throw new DomainException($"Fee {fee.FeeNo} is for nothing; there is nothing to pay.");
    var payment = Create(id, channel, fee.Amount, transactionRef, receiptNo, bankCode, paidAt, receivedBy, now);
    payment.TaxFileId = file?.Id;
    payment.Psid = file?.Psid;
    payment.PartyId = fee.PartyId;
    payment.FeeId = fee.Id;
    payment.PayerName = Clip(payerName);
    payment._allocations.Add(PaymentAllocation.ForFee(PaymentAllocationId.New(), id, fee));
    fee.MarkPaid();
    return payment;
  }

  private static Payment Create(PaymentId id, PaymentChannel channel, decimal amount, string? transactionRef, string? receiptNo, string? bankCode, DateTime paidAt,
      Guid? receivedBy, DateTime now)
  {
    var value = Rupees.Round(amount);
    if (value <= 0)
      throw new DomainException("The amount must be positive.");
    if (paidAt > now.AddMinutes(5))
      throw new DomainException("A payment cannot be dated in the future.");

    var manual = IsManual(channel);
    var receipt = Guard.Text(receiptNo, 40, "Receipt no.");
    var reference = Guard.Text(transactionRef, 60, "Transaction reference");
    if (manual && receipt is null)
      throw new DomainException("A cash or bank-counter payment needs the manual receipt number.");
    if (!manual && reference is null)
      throw new DomainException("A 1Link, QR or online payment needs the bank's transaction reference.");

    return new Payment
    {
      Id = id,
      Channel = channel,
      Amount = value,
      ReceiptNo = receipt,
      ReceivedBy = manual ? receivedBy : null,
      BankCode = Guard.Text(bankCode, 20, "Bank code"),
      TransactionRef = reference,
      PaidAt = paidAt,
      PostedAt = now,
      Status = PaymentStatus.Posted
    };
  }

  public void Allocate(IReadOnlyList<ItemSettlement> settled)
  {
    foreach (var s in settled)
      _allocations.Add(PaymentAllocation.Create(PaymentAllocationId.New(), Id, s));
  }

  public decimal Allocated => _allocations.Sum(a => a.Amount);

  /// Part of what an allocation paid moves to other items (a credit carried to the next year's demand): the allocation
  /// keeps the rest, the items it moves to get their own rows. The payment's total does not change.
  public void MoveCredit(PaymentAllocationId from, decimal amount, IReadOnlyList<ItemSettlement> into)
  {
    var allocation = _allocations.FirstOrDefault(a => a.Id == from) ?? throw new DomainException("The allocation is not part of this payment.");
    if (into.Sum(i => i.Amount) != amount || amount <= 0 || amount > allocation.Amount)
      throw new DomainException("A credit moves whole: what leaves one item goes to others.");
    allocation.Reduce(amount);
    if (allocation.Amount == 0)
      _allocations.Remove(allocation);
    Allocate(into);
  }

  /// Takes the payment back with a reason; what it paid is owed again (worked out by the caller item by item).
  public void Reverse(string reason, Guid actor, DateTime now)
  {
    if (Status == PaymentStatus.Reversed)
      throw new DomainException("The payment was already reversed.");
    ReversalReason = Guard.RequiredText(reason, 300, "Reason");
    ReversedBy = actor;
    ReversedAt = now;
    Status = PaymentStatus.Reversed;
  }

  private static string? Clip(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim().Length <= 200 ? name.Trim() : name.Trim()[..200];
}

/// taxation.payment_allocation: the part of a payment that settled one item (a demand line, an arrears row, a penalty or a fee).
public class PaymentAllocation : Entity<PaymentAllocationId>
{
  public PaymentId PaymentId { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public DemandLineId? DemandLineId { get; private set; }
  public ArrearsId? ArrearsId { get; private set; }
  public PenaltyId? PenaltyId { get; private set; }
  public FeeId? FeeId { get; private set; }
  public decimal Amount { get; private set; }

  private PaymentAllocation() { }

  public Guid ItemId => DemandLineId?.Value ?? ArrearsId?.Value ?? PenaltyId?.Value ?? FeeId?.Value ?? Guid.Empty;

  internal static PaymentAllocation Create(PaymentAllocationId id, PaymentId paymentId, ItemSettlement s) => new()
  {
    Id = id,
    PaymentId = paymentId,
    HeadId = s.HeadId,
    DemandLineId = s.Kind == DemandItemKind.Line ? global::DemandLineId.Of(s.ItemId) : null,
    ArrearsId = s.Kind == DemandItemKind.Arrears ? global::ArrearsId.Of(s.ItemId) : null,
    PenaltyId = s.Kind == DemandItemKind.Penalty ? global::PenaltyId.Of(s.ItemId) : null,
    Amount = s.Amount
  };

  internal static PaymentAllocation ForFee(PaymentAllocationId id, PaymentId paymentId, Fee fee) => new()
  {
    Id = id,
    PaymentId = paymentId,
    HeadId = fee.HeadId,
    FeeId = fee.Id,
    Amount = fee.Amount
  };

  internal void Reduce(decimal amount) => Amount -= amount;
}

/// taxation.party: a payer of a fee who is not on a tax file (contractor, vendor, guide, applicant, staff).
public class Party : Aggregate<PartyId>
{
  public PartyType PartyType { get; private set; }
  public Guid? OwnerId { get; private set; }
  public Guid? EmployeeId { get; private set; }
  public string Name { get; private set; } = default!;
  public string? CnicNtn { get; private set; }
  public string? Mobile { get; private set; }
  public string? Address { get; private set; }
  public bool IsActive { get; private set; }

  private Party() { }

  public static Party Create(PartyId id, PartyType type, string name, string? cnicNtn, string? mobile, string? address, Guid? ownerId, Guid? employeeId)
  {
    var party = new Party { Id = id, PartyType = type, IsActive = true };
    party.Update(name, cnicNtn, mobile, address, ownerId, employeeId);
    return party;
  }

  public void Update(string name, string? cnicNtn, string? mobile, string? address, Guid? ownerId, Guid? employeeId)
  {
    Name = Guard.RequiredText(name, 200, "Name");
    CnicNtn = Guard.CnicNtn(cnicNtn, "CNIC / NTN");
    Mobile = Guard.Text(mobile, 30, "Mobile");
    Address = Guard.Text(address, 400, "Address");
    if (PartyType == PartyType.Owner && ownerId is null)
      throw new DomainException("An owner party points at the Property owner.");
    if (PartyType == PartyType.Employee && employeeId is null)
      throw new DomainException("An employee party points at the HRM employee.");
    OwnerId = ownerId;
    EmployeeId = employeeId;
  }

  /// A party no longer dealt with is kept for its fees and receipts but not offered for new ones.
  public void SetActive(bool isActive) => IsActive = isActive;
}

/// taxation.fee: a case-by-case charge (transfer, building plan, violation, non-utilization, water connection, malba
/// security, enlistment, registration, licence, copies), worked out from the rate set and recorded DUE or PAID.
public class Fee : Aggregate<FeeId>
{
  public string FeeNo { get; private set; } = default!;
  public TaxHeadId HeadId { get; private set; } = default!;
  public TaxFileId? TaxFileId { get; private set; }
  public PartyId? PartyId { get; private set; }
  public FeeSourceType? SourceType { get; private set; }
  public Guid? SourceId { get; private set; }
  public string? SourceNo { get; private set; }
  public decimal? BasisQty { get; private set; }
  public string? BasisUnit { get; private set; }
  public RateBandId? RateBandId { get; private set; }
  public decimal Amount { get; private set; }
  public string? ExemptionReason { get; private set; }
  public FeeStatus Status { get; private set; }

  /// Concurrency token (Postgres xmin): a fee paid twice at once is paid once.
  public uint Version { get; private set; }

  private Fee() { }

  /// A fee charged DUE (or waived at once with the exemption reason); it is paid by its receipt.
  public static Fee Charge(FeeId id, string feeNo, TaxHead head, TaxFileId? fileId, PartyId? partyId, FeeSourceType? sourceType, Guid? sourceId, string? sourceNo,
      decimal? qty, string? unit, RateBandId? bandId, decimal amount, string? exemptionReason)
  {
    if (head.HeadKind != HeadKind.Other)
      throw new DomainException($"{head.Name} is part of the annual demand, not a fee.");
    if (head.ChargedOnNotice)
      throw new DomainException($"{head.Name} is charged on notices (as an earlier due), not as a fee.");
    if (!head.IsActive)
      throw new DomainException($"{head.Name} is inactive.");
    if (fileId is null && partyId is null)
      throw new DomainException("A fee is charged to a tax file or to a party.");
    var exempt = Guard.Text(exemptionReason, 200, "Exemption reason");
    return new Fee
    {
      Id = id,
      FeeNo = feeNo,
      HeadId = head.Id,
      TaxFileId = fileId,
      PartyId = partyId,
      SourceType = sourceType,
      SourceId = sourceId,
      SourceNo = Guard.Text(sourceNo, 50, "Source no."),
      BasisQty = qty,
      BasisUnit = unit is null ? null : unit.Length <= 10 ? unit : unit[..10],
      RateBandId = bandId,
      Amount = exempt is null ? Rupees.Round(Guard.NotNegative(amount, "Amount")) : 0m,
      ExemptionReason = exempt,
      Status = exempt is not null ? FeeStatus.Waived : FeeStatus.Due
    };
  }

  /// Paid by its receipt (a payment row for the fee).
  internal void MarkPaid()
  {
    if (Status != FeeStatus.Due)
      throw new DomainException($"Fee {FeeNo} is {EnumText.Words(Status)}.");
    Status = FeeStatus.Paid;
  }

  /// Its receipt was reversed: the fee is due again.
  public void Unpay()
  {
    if (Status != FeeStatus.Paid)
      throw new DomainException($"Fee {FeeNo} is {EnumText.Words(Status)}, not paid.");
    Status = FeeStatus.Due;
  }

  public void Waive(string reason)
  {
    if (Status != FeeStatus.Due)
      throw new DomainException($"Fee {FeeNo} is {EnumText.Words(Status)}; only a due fee can be waived.");
    ExemptionReason = Guard.RequiredText(reason, 200, "Reason");
    Status = FeeStatus.Waived;
  }
}

/// taxation.clearance: the certificate Property needs before a transfer or a building plan: issued only when nothing is
/// outstanding on the file, valid for the configured days, consumed once Property uses it.
public class Clearance : Aggregate<ClearanceId>
{
  public string ClearanceNo { get; private set; } = default!;
  public TaxFileId TaxFileId { get; private set; } = default!;
  /// Who the file's dues were addressed to on the day it was issued, as printed.
  public string? PayerName { get; private set; }
  public ClearancePurpose Purpose { get; private set; }
  public Guid? PropertyRefId { get; private set; }
  public string? PropertyRefNo { get; private set; }
  public decimal OutstandingChecked { get; private set; }
  public FeeId? FeeId { get; private set; }
  public DateOnly IssuedOn { get; private set; }
  public DateOnly ValidUntil { get; private set; }
  public Guid? IssuedBy { get; private set; }
  public DateTime? ConsumedAt { get; private set; }
  public Guid? ConsumedBy { get; private set; }
  public Guid? RevokedBy { get; private set; }
  public DateTime? RevokedAt { get; private set; }
  public ClearanceStatus Status { get; private set; }

  /// Concurrency token (Postgres xmin): a certificate used by two transfers at once is used once.
  public uint Version { get; private set; }

  private Clearance() { }

  /// Issued only when nothing is owed on the file - no balance behind its reference and no fee due on it. Valid for
  /// validDays days counting the day of issue (30 days: issued 1 Oct, last valid day 30 Oct).
  public static Clearance Issue(ClearanceId id, string clearanceNo, TaxFile file, string? payerName, ClearancePurpose purpose, decimal dues, decimal feesDue,
      Guid? propertyRefId, string? propertyRefNo, FeeId? feeId, DateOnly today, int validDays, Guid actor)
  {
    if (dues > 0 || feesDue > 0)
    {
      var parts = new List<string>();
      if (dues > 0) parts.Add($"{Rupees.Text(dues)} behind the reference");
      if (feesDue > 0) parts.Add($"{Rupees.Text(feesDue)} in fees due");
      throw new DomainException($"{Rupees.Text(dues + feesDue)} is outstanding on {file.FileNo} ({string.Join(" and ", parts)}). A clearance is issued only when nothing is due.");
    }
    if (validDays < 1)
      throw new DomainException("A clearance is valid for at least one day.");
    return new Clearance
    {
      Id = id,
      ClearanceNo = clearanceNo,
      TaxFileId = file.Id,
      PayerName = string.IsNullOrWhiteSpace(payerName) ? null : payerName.Trim().Length <= 200 ? payerName.Trim() : payerName.Trim()[..200],
      Purpose = purpose,
      PropertyRefId = propertyRefId,
      PropertyRefNo = Guard.Text(propertyRefNo, 50, "Reference no."),
      OutstandingChecked = 0m,
      FeeId = feeId,
      IssuedOn = today,
      ValidUntil = today.AddDays(validDays - 1),
      IssuedBy = actor,
      Status = ClearanceStatus.Issued
    };
  }

  public ClearanceStatus StatusOn(DateOnly today) => Status == ClearanceStatus.Issued && today > ValidUntil ? ClearanceStatus.Expired : Status;

  /// Why the certificate cannot be used today, in words; null when it can.
  public string? Unusable(DateOnly today) => StatusOn(today) switch
  {
    ClearanceStatus.Consumed => $"Clearance {ClearanceNo} was already used{(ConsumedAt is { } at ? $" on {at:dd MMM yyyy}" : "")}; a clearance is used once.",
    ClearanceStatus.Expired => $"Clearance {ClearanceNo} expired after {ValidUntil:dd MMM yyyy}. Issue a new one.",
    ClearanceStatus.Revoked => $"Clearance {ClearanceNo} was revoked; it cannot be used.",
    _ => null
  };

  /// Used by the transfer or building plan it was issued for (once). A purpose given must be the one it was issued for;
  /// the process's own record (transfer or plan id and no.) is kept when given.
  public void Consume(DateOnly today, DateTime now, Guid? actor, ClearancePurpose? purpose = null, Guid? propertyRefId = null, string? propertyRefNo = null)
  {
    if (Unusable(today) is { } why)
      throw new DomainException(why);
    if (purpose is { } p && p != Purpose)
      throw new DomainException($"Clearance {ClearanceNo} was issued for {Words(Purpose)}, not for {Words(p)}.");
    if (propertyRefId is not null)
      PropertyRefId = propertyRefId;
    if (Guard.Text(propertyRefNo, 50, "Reference no.") is { } no)
      PropertyRefNo = no;
    Status = ClearanceStatus.Consumed;
    ConsumedAt = now;
    ConsumedBy = actor;
  }

  public void Revoke(DateOnly today, Guid actor, DateTime now)
  {
    if (Status != ClearanceStatus.Issued)
      throw new DomainException(Unusable(today) ?? $"Clearance {ClearanceNo} cannot be revoked.");
    Status = ClearanceStatus.Revoked;
    RevokedBy = actor;
    RevokedAt = now;
  }

  private static string Words(ClearancePurpose purpose) => purpose == ClearancePurpose.Transfer ? "a transfer" : "a building plan";
}

/// taxation.sms_message: an SMS queued for the payer. No SMS gateway is connected yet; rows stay QUEUED.
public class SmsMessage : Aggregate<SmsMessageId>
{
  public TaxFileId? TaxFileId { get; private set; }
  public DemandId? DemandId { get; private set; }
  public NoticeId? NoticeId { get; private set; }
  public Guid? OwnerId { get; private set; }
  public string ToMobile { get; private set; } = default!;
  public MessageTemplateId TemplateId { get; private set; } = default!;
  public string Body { get; private set; } = default!;
  public DateTime QueuedAt { get; private set; }
  public DateTime? SentAt { get; private set; }
  public string? ProviderRef { get; private set; }
  public SmsStatus? DeliveryStatus { get; private set; }

  private SmsMessage() { }

  public static SmsMessage Queue(SmsMessageId id, TaxFileId? fileId, DemandId? demandId, NoticeId? noticeId, Guid? ownerId, string toMobile, MessageTemplate template,
      string body, DateTime now) => new()
  {
    Id = id,
    TaxFileId = fileId,
    DemandId = demandId,
    NoticeId = noticeId,
    OwnerId = ownerId,
    ToMobile = Guard.RequiredText(toMobile, 30, "Mobile"),
    TemplateId = template.Id,
    Body = Guard.RequiredText(body, 2000, "Text"),
    QueuedAt = now,
    DeliveryStatus = SmsStatus.Queued
  };
}
