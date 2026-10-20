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

/// taxation.payment: money received against a property's constant reference, settled oldest year first, item by item
/// (payment_allocation). 1Link, QR and online payments carry the bank's transaction reference, which makes posting
/// idempotent; cash and bank-counter payments carry the Section's manual receipt number.
public class Payment : Aggregate<PaymentId>
{
  private readonly List<PaymentAllocation> _allocations = new();

  public TaxFileId TaxFileId { get; private set; } = default!;
  public string Psid { get; private set; } = default!;
  public Guid? ChallanId { get; private set; }
  public PaymentChannel Channel { get; private set; }
  public string? ReceiptNo { get; private set; }
  public Guid? ReceivedBy { get; private set; }
  public string? BankCode { get; private set; }
  public string TransactionRef { get; private set; } = default!;
  public DateTime PaidAt { get; private set; }
  public decimal Amount { get; private set; }
  public DateTime? PostedAt { get; private set; }
  public DateTime? AccountsPostedAt { get; private set; }
  public PaymentStatus Status { get; private set; }

  public IReadOnlyList<PaymentAllocation> Allocations => _allocations.AsReadOnly();

  public static bool IsManual(PaymentChannel channel) => channel is PaymentChannel.Cash or PaymentChannel.BankCounter;

  private Payment() { }

  public static Payment Receive(PaymentId id, TaxFile file, PaymentChannel channel, decimal amount, string? transactionRef, string? receiptNo, string? bankCode,
      DateTime paidAt, Guid? receivedBy, DateTime now)
  {
    if (file.Psid is null)
      throw new DomainException($"{file.FileNo} has no 1Link reference.");
    var value = Rupees.Round(amount);
    if (value <= 0)
      throw new DomainException("The amount must be positive.");
    if (paidAt > now.AddMinutes(5))
      throw new DomainException("A payment cannot be dated in the future.");

    var manual = IsManual(channel);
    if (manual && string.IsNullOrWhiteSpace(receiptNo))
      throw new DomainException("A cash or bank-counter payment needs the manual receipt number.");
    if (!manual && string.IsNullOrWhiteSpace(transactionRef))
      throw new DomainException("A 1Link, QR or online payment needs the bank's transaction reference.");

    return new Payment
    {
      Id = id,
      TaxFileId = file.Id,
      Psid = file.Psid,
      Channel = channel,
      Amount = value,
      ReceiptNo = Guard.Text(receiptNo, 40, "Receipt no."),
      ReceivedBy = manual ? receivedBy : null,
      BankCode = Guard.Text(bankCode, 20, "Bank code"),
      TransactionRef = Guard.Text(transactionRef, 60, "Transaction reference") ?? $"MAN-{id.Value:N}",
      PaidAt = paidAt,
      PostedAt = now,
      Status = PaymentStatus.Posted
    };
  }

  public void Allocate(Demand demand, IReadOnlyList<ItemSettlement> settled)
  {
    foreach (var s in settled)
      _allocations.Add(PaymentAllocation.Create(PaymentAllocationId.New(), Id, s));
  }

  public decimal Allocated => _allocations.Sum(a => a.Amount);
}

/// taxation.payment_allocation: the part of a payment that settled one item (a demand line, an arrears row or a penalty).
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

  private Party() { }

  public static Party Create(PartyId id, PartyType type, string name, string? cnicNtn, string? mobile, string? address, Guid? ownerId, Guid? employeeId)
  {
    var party = new Party { Id = id, PartyType = type };
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

  private Fee() { }

  public static Fee Charge(FeeId id, string feeNo, TaxHead head, TaxFileId? fileId, PartyId? partyId, FeeSourceType? sourceType, Guid? sourceId, string? sourceNo,
      decimal? qty, string? unit, RateBandId? bandId, decimal amount, string? exemptionReason, bool paid)
  {
    if (head.HeadKind != HeadKind.Other)
      throw new DomainException($"{head.Name} is part of the annual demand, not a fee.");
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
      Status = exempt is not null ? FeeStatus.Waived : paid ? FeeStatus.Paid : FeeStatus.Due
    };
  }

  public void MarkPaid()
  {
    if (Status != FeeStatus.Due)
      throw new DomainException($"Fee {FeeNo} is {EnumText.Words(Status)}.");
    Status = FeeStatus.Paid;
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
  public ClearancePurpose Purpose { get; private set; }
  public Guid? PropertyRefId { get; private set; }
  public string? PropertyRefNo { get; private set; }
  public decimal OutstandingChecked { get; private set; }
  public FeeId? FeeId { get; private set; }
  public DateOnly IssuedOn { get; private set; }
  public DateOnly ValidUntil { get; private set; }
  public Guid? IssuedBy { get; private set; }
  public DateTime? ConsumedAt { get; private set; }
  public ClearanceStatus Status { get; private set; }

  private Clearance() { }

  public static Clearance Issue(ClearanceId id, string clearanceNo, TaxFile file, ClearancePurpose purpose, decimal outstanding, Guid? propertyRefId, string? propertyRefNo,
      FeeId? feeId, DateOnly today, int validDays, Guid actor)
  {
    if (outstanding > 0)
      throw new DomainException($"{Rupees.Text(outstanding)} is outstanding on {file.FileNo}. A clearance is issued only when nothing is due.");
    return new Clearance
    {
      Id = id,
      ClearanceNo = clearanceNo,
      TaxFileId = file.Id,
      Purpose = purpose,
      PropertyRefId = propertyRefId,
      PropertyRefNo = Guard.Text(propertyRefNo, 50, "Reference no."),
      OutstandingChecked = 0m,
      FeeId = feeId,
      IssuedOn = today,
      ValidUntil = today.AddDays(validDays),
      IssuedBy = actor,
      Status = ClearanceStatus.Issued
    };
  }

  public ClearanceStatus StatusOn(DateOnly today) => Status == ClearanceStatus.Issued && today > ValidUntil ? ClearanceStatus.Expired : Status;

  public void Consume(DateOnly today, DateTime now)
  {
    if (StatusOn(today) != ClearanceStatus.Issued)
      throw new DomainException($"Clearance {ClearanceNo} is {EnumText.Words(StatusOn(today))}.");
    Status = ClearanceStatus.Consumed;
    ConsumedAt = now;
  }

  public void Revoke()
  {
    if (Status != ClearanceStatus.Issued)
      throw new DomainException($"Clearance {ClearanceNo} is {EnumText.Words(Status)}.");
    Status = ClearanceStatus.Revoked;
  }
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
