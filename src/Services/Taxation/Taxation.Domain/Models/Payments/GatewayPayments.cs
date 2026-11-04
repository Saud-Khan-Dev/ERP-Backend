/// The facts of one payment, as a gateway reported them and its adapter proved them genuine. EventId identifies the
/// report itself: the same payment sent twice carries the same EventId.
public sealed record GatewayPaymentFacts(string EventType, string EventId, PaymentChannel Channel, string Psid, decimal Amount, string TransactionRef, DateTime PaidAt, string? BankCode);

/// taxation.integration_inbox: a payment a gateway proved genuine, kept before anything is done with it, so a crash, a
/// refusal or a retry never loses it. One row per (source, event_id), which makes a repeated report harmless. Only the
/// facts are kept, never the raw message or anything secret. The money is real by the time a row exists, so a row that
/// cannot be applied is HELD for the Section, never dropped.
public class GatewayNotification : Aggregate<GatewayNotificationId>
{
  public string Source { get; private set; } = default!;
  public string EventType { get; private set; } = default!;
  public string EventId { get; private set; } = default!;
  public PaymentChannel Channel { get; private set; }
  public string Psid { get; private set; } = default!;
  public decimal Amount { get; private set; }
  public string TransactionRef { get; private set; } = default!;
  public string? BankCode { get; private set; }
  public DateTime PaidAt { get; private set; }
  public DateTime ReceivedAt { get; private set; }
  public DateTime? ProcessedAt { get; private set; }
  public GatewayNotificationStatus Status { get; private set; }
  public TaxFileId? TaxFileId { get; private set; }
  public PaymentId? PaymentId { get; private set; }
  public string? Error { get; private set; }

  /// Concurrency token (Postgres xmin).
  public uint Version { get; private set; }

  public bool IsOpen => Status is GatewayNotificationStatus.Received or GatewayNotificationStatus.Held;

  /// A report normally takes seconds; one still RECEIVED after this long stopped half way and waits for the Section.
  public const int StuckMinutes = 5;

  private GatewayNotification() { }

  public static GatewayNotification Record(GatewayNotificationId id, string source, GatewayPaymentFacts facts, DateTime now)
  {
    if (Payment.IsManual(facts.Channel))
      throw new DomainException("A gateway reports bank payments, not cash or counter receipts.");
    if (facts.Amount <= 0)
      throw new DomainException("A gateway payment must be for a positive amount.");
    return new GatewayNotification
    {
      Id = id,
      Source = Guard.RequiredText(source, 30, "Source"),
      EventType = Guard.RequiredText(facts.EventType, 80, "Event type"),
      EventId = Guard.RequiredText(facts.EventId, 80, "Event id"),
      Channel = facts.Channel,
      Psid = Digits(facts.Psid),
      Amount = facts.Amount,
      TransactionRef = Guard.RequiredText(facts.TransactionRef, 60, "Transaction reference"),
      BankCode = Guard.Text(facts.BankCode, 20, "Bank code"),
      PaidAt = Stored(facts.PaidAt),
      ReceivedAt = now,
      Status = GatewayNotificationStatus.Received
    };
  }

  /// A repeated report must say what the first one said; anything else is not a repeat, and is refused.
  public bool SameFacts(string source, GatewayPaymentFacts facts) =>
      Source == source.Trim() && Channel == facts.Channel && Psid == Digits(facts.Psid) && Amount == facts.Amount
      && TransactionRef == facts.TransactionRef.Trim() && PaidAt == Stored(facts.PaidAt);

  public void MarkPosted(PaymentId paymentId, TaxFileId taxFileId, DateTime now)
  {
    EnsureOpen();
    PaymentId = paymentId;
    TaxFileId = taxFileId;
    Status = GatewayNotificationStatus.Posted;
    ProcessedAt = now;
    Error = null;
  }

  public void Hold(string reason, TaxFileId? taxFileId, DateTime now)
  {
    EnsureOpen();
    Error = Guard.RequiredText(reason, 500, "Reason");
    TaxFileId = taxFileId ?? TaxFileId;
    Status = GatewayNotificationStatus.Held;
    ProcessedAt = now;
  }

  public void Dismiss(string reason, DateTime now)
  {
    EnsureOpen();
    Error = Guard.RequiredText(reason, 500, "Reason");
    Status = GatewayNotificationStatus.Dismissed;
    ProcessedAt = now;
  }

  private void EnsureOpen()
  {
    if (!IsOpen)
      throw new DomainException($"The gateway payment {TransactionRef} is already {EnumText.Words(Status)}.");
  }

  /// The time as the database keeps it (to the microsecond), so a repeat compares equal with what was stored.
  private static DateTime Stored(DateTime utc) => utc.Kind == DateTimeKind.Utc
    ? new DateTime(utc.Ticks - utc.Ticks % 10, DateTimeKind.Utc)
    : throw new DomainException("A gateway payment's time must be given in UTC.");

  private static string Digits(string psid)
  {
    var digits = new string((psid ?? "").Where(char.IsAsciiDigit).ToArray());
    return digits.Length is > 0 and <= 30 ? digits : throw new DomainException("A gateway payment needs the 1Link reference.");
  }
}

/// A line of the bank's settlement report, as read by the adapter or entered from it.
public sealed record SettlementLineInput(string TransactionRef, string? Psid, decimal Amount, DateTime? PaidAt);

/// What is on record for a transaction, for comparing with the bank's line.
public sealed record SettledPayment(PaymentId Id, decimal Amount, string? Psid, PaymentStatus Status);

/// taxation.settlement_batch: one settlement report from the bank, compared line by line with the payments on record when
/// it is imported. It proves the money reached GDA's account. It changes no payment: a difference is a line for the
/// Section to look into.
public class SettlementBatch : Aggregate<SettlementBatchId>
{
  private readonly List<SettlementLine> _lines = new();

  public PaymentChannel Channel { get; private set; }
  public string BatchRef { get; private set; } = default!;
  public DateOnly SettlementDate { get; private set; }
  public DateOnly CoversFrom { get; private set; }
  public DateOnly CoversTo { get; private set; }
  public DateTime ImportedAt { get; private set; }
  public decimal TotalAmount { get; private set; }
  public int LineCount { get; private set; }
  public int MatchedCount { get; private set; }
  public int ExceptionCount { get; private set; }

  public IReadOnlyList<SettlementLine> Lines => _lines.AsReadOnly();

  private SettlementBatch() { }

  public static SettlementBatch Import(SettlementBatchId id, PaymentChannel channel, string batchRef, DateOnly settlementDate, DateOnly coversFrom, DateOnly coversTo,
      IReadOnlyList<SettlementLineInput> lines, IReadOnlyDictionary<string, SettledPayment> payments, IReadOnlySet<string> settledBefore, DateTime now)
  {
    if (Payment.IsManual(channel))
      throw new DomainException("A settlement report covers bank payments, not cash or counter receipts.");
    if (coversTo < coversFrom)
      throw new DomainException("The period of the report ends before it starts.");
    if (settlementDate < coversFrom)
      throw new DomainException("The bank cannot settle a period before it starts.");
    if (settlementDate > DateOnly.FromDateTime(now.AddHours(5)))
      throw new DomainException("The settlement date is still to come; a report is imported once the bank has settled.");
    if (lines.Count == 0)
      throw new DomainException("The settlement report has no lines.");
    var repeated = lines.GroupBy(l => (l.TransactionRef ?? "").Trim()).FirstOrDefault(g => g.Count() > 1);
    if (repeated is not null)
      throw new DomainException($"Transaction {repeated.Key} is on the report twice.");

    var batch = new SettlementBatch
    {
      Id = id,
      Channel = channel,
      BatchRef = Guard.RequiredText(batchRef, 60, "Batch reference"),
      SettlementDate = settlementDate,
      CoversFrom = coversFrom,
      CoversTo = coversTo,
      ImportedAt = now
    };
    foreach (var line in lines)
    {
      var reference = (line.TransactionRef ?? "").Trim();
      batch._lines.Add(SettlementLine.Compare(SettlementLineId.New(), id, line, payments.GetValueOrDefault(reference), settledBefore.Contains(reference)));
    }
    batch.TotalAmount = batch._lines.Sum(l => l.Amount);
    batch.LineCount = batch._lines.Count;
    batch.MatchedCount = batch._lines.Count(l => l.Match == SettlementMatch.Matched);
    batch.ExceptionCount = batch.LineCount - batch.MatchedCount;
    return batch;
  }
}

/// taxation.settlement_line: one transaction of a settlement report and how it compares with the payment on record. A
/// transaction is matched once, across all reports (settlement_line_matched_key).
public class SettlementLine : Entity<SettlementLineId>
{
  public SettlementBatchId BatchId { get; private set; } = default!;
  public string TransactionRef { get; private set; } = default!;
  public string? Psid { get; private set; }
  public decimal Amount { get; private set; }
  public DateTime? PaidAt { get; private set; }
  public SettlementMatch Match { get; private set; }
  public PaymentId? PaymentId { get; private set; }
  public string? Note { get; private set; }

  private SettlementLine() { }

  internal static SettlementLine Compare(SettlementLineId id, SettlementBatchId batchId, SettlementLineInput input, SettledPayment? payment, bool settledBefore)
  {
    if (input.Amount <= 0)
      throw new DomainException($"Transaction {input.TransactionRef} has no positive amount.");
    var psid = new string((input.Psid ?? "").Where(char.IsAsciiDigit).ToArray());
    var line = new SettlementLine
    {
      Id = id,
      BatchId = batchId,
      TransactionRef = Guard.RequiredText(input.TransactionRef, 60, "Transaction reference"),
      Psid = psid.Length == 0 ? null : Guard.Text(psid, 30, "Reference"),
      Amount = input.Amount,
      PaidAt = input.PaidAt is { } paidAt ? AsUtc(paidAt) : null,
      PaymentId = payment?.Id
    };
    (line.Match, line.Note) = payment switch
    {
      _ when settledBefore => (SettlementMatch.AlreadySettled, "The bank settled this transaction on an earlier report."),
      null => (SettlementMatch.NotInErp, "The bank settled it but no payment is on record."),
      { Status: PaymentStatus.Reversed } => (SettlementMatch.ReversedInErp, "The bank settled it but the payment was reversed here."),
      _ when payment.Amount != input.Amount => (SettlementMatch.AmountDiffers, $"The bank settled {Rupees.Text(input.Amount)}; {Rupees.Text(payment.Amount)} is on record."),
      _ when line.Psid is not null && payment.Psid != line.Psid => (SettlementMatch.ReferenceDiffers, $"The bank settled it on {line.Psid}; it is on record on {payment.Psid}."),
      _ => (SettlementMatch.Matched, null)
    };
    return line;
  }

  /// A report's time as the database keeps it: one given without a zone is Pakistan time, as the bank prints it.
  private static DateTime AsUtc(DateTime time) => time.Kind switch
  {
    DateTimeKind.Utc => time,
    DateTimeKind.Local => time.ToUniversalTime(),
    _ => DateTime.SpecifyKind(time, DateTimeKind.Utc).AddHours(-5)
  };
}
