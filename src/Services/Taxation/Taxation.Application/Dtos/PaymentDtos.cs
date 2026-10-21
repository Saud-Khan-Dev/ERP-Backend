// ---- payments, references, messages ----

/// FiscalYear: the year of the line, row or surcharge it settled (null for a fee or an earlier due of no year).
public sealed record AllocationDto(Guid Id, Guid HeadId, string HeadCode, string HeadName, Guid? DemandLineId, Guid? ArrearsId, Guid? PenaltyId, Guid? FeeId, int? FiscalYear,
  decimal Amount);

/// Kind: what it paid (the dues behind a reference, or a fee). PayerName: who paid, as on the day (the receipt reprints it).
/// ReceivedBy / ReversedBy: user ids (the screens show the names).
public sealed record PaymentDto(
  Guid Id, PaymentKind Kind, Guid? TaxFileId, string? FileNo, string? Psid, Guid? PartyId, string? PartyName, Guid? FeeId, string? FeeNo, string? PayerName,
  PaymentChannel Channel, string? ReceiptNo, Guid? ReceivedBy, string? BankCode, string? TransactionRef, DateTime PaidAt, decimal Amount, DateTime? PostedAt,
  PaymentStatus Status, DateTime? ReversedAt, Guid? ReversedBy, string? ReversalReason, IReadOnlyList<AllocationDto> Allocations);

/// AlreadyPosted: the transaction reference was posted before; nothing was changed (posting is idempotent).
public sealed record ReceivePaymentResult(Guid PaymentId, bool AlreadyPosted, decimal Amount, decimal DuesBefore, decimal DuesAfter, IReadOnlyList<Guid> DemandIds,
  string? ReceiptNo);

public sealed record ReferenceYearDto(Guid DemandId, string DemandNo, int FiscalYear, string FiscalYearLabel, DemandStatus Status, decimal Balance, DateOnly? DueDate);

/// What is behind a 1Link reference (a bill inquiry).
/// OpeningArrears: what the old register's dues still owe before the file's first demand (paid against the reference too).
public sealed record ReferenceDto(
  string Psid, string PsidFormatted, Guid TaxFileId, string FileNo, string PropertyCode, string? PayerName, decimal AmountDue, DateOnly? DueDate,
  IReadOnlyList<ReferenceYearDto> Years, decimal OpeningArrears);

public sealed record SmsDto(Guid Id, Guid? TaxFileId, string? FileNo, Guid? DemandId, Guid? NoticeId, string ToMobile, string TemplateCode, string Body, DateTime QueuedAt,
  DateTime? SentAt, SmsStatus? DeliveryStatus);

// ---- fees, parties, clearances ----

/// FeeCount / FeesDue: the fees charged to the party and what is still due on them.
public sealed record PartyDto(Guid Id, PartyType PartyType, string Name, string? CnicNtn, string? Mobile, string? Address, Guid? OwnerId, Guid? EmployeeId, bool IsActive,
  int FeeCount, decimal FeesDue, DateTime? CreatedAt);

/// ChargedBy: a user id. PaymentId / ReceiptNo / PaidAt: the receipt that paid it (none while due or waived).
public sealed record FeeDto(
  Guid Id, string FeeNo, Guid HeadId, string HeadCode, string HeadName, Guid? TaxFileId, string? FileNo, Guid? PartyId, string? PartyName, FeeSourceType? SourceType,
  Guid? SourceId, string? SourceNo, decimal? BasisQty, string? BasisUnit, decimal Amount, string? ExemptionReason, FeeStatus Status, DateTime? CreatedAt, Guid? ChargedBy,
  Guid? PaymentId, string? ReceiptNo, DateTime? PaidAt);

public sealed record FeeQuoteDto(string HeadCode, string HeadName, decimal Amount, string Explanation, decimal? Quantity, string? Unit, Guid? RateBandId, string RateSetName);

/// PropertyId / PropertyCode: the Property property the certificate is for (its tax file's).
/// PayerName: who the dues were addressed to on the day it was issued (as printed). IssuedBy / ConsumedBy / RevokedBy: user ids.
public sealed record ClearanceDto(
  Guid Id, string ClearanceNo, Guid TaxFileId, string FileNo, Guid PropertyId, string PropertyCode, string? PayerName, ClearancePurpose Purpose, Guid? PropertyRefId,
  string? PropertyRefNo, decimal OutstandingChecked, Guid? FeeId, DateOnly IssuedOn, DateOnly ValidUntil, Guid? IssuedBy, DateTime? ConsumedAt, Guid? ConsumedBy,
  DateTime? RevokedAt, Guid? RevokedBy, ClearanceStatus Status);

// ---- reports ----

public sealed record CircleCollectionDto(Guid CircleId, string CircleCode, int Demands, int Paid, decimal Demanded, decimal Recovered, decimal Outstanding);

/// The year at a glance: what was demanded (this year's charges, arrears billed and surcharges), recovered, outstanding,
/// by circle, and the surcharges applied.
public sealed record CollectionSummaryDto(
  int FiscalYear, string FiscalYearLabel, int TaxFiles, int Demands, int Paid, int PartPaid, int Unpaid, int Defaulted, decimal Demanded, decimal Recovered,
  int RecoveryPct, decimal OutstandingAllYears, decimal IntraYearSurcharges, decimal YearEndSurcharges, int PendingNotices,
  IReadOnlyList<CircleCollectionDto> ByCircle);
