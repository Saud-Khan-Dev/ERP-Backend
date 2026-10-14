// ---- payments, references, messages ----

public sealed record AllocationDto(Guid Id, Guid HeadId, string HeadCode, string HeadName, Guid? DemandLineId, Guid? ArrearsId, Guid? PenaltyId, Guid? FeeId, decimal Amount);

public sealed record PaymentDto(
  Guid Id, Guid TaxFileId, string FileNo, string Psid, PaymentChannel Channel, string? ReceiptNo, Guid? ReceivedBy, string? BankCode, string TransactionRef,
  DateTime PaidAt, decimal Amount, DateTime? PostedAt, PaymentStatus Status, IReadOnlyList<AllocationDto> Allocations);

/// AlreadyPosted: the transaction reference was posted before; nothing was changed (posting is idempotent).
public sealed record ReceivePaymentResult(Guid PaymentId, bool AlreadyPosted, decimal Amount, decimal DuesBefore, decimal DuesAfter, IReadOnlyList<Guid> DemandIds);

public sealed record ReferenceYearDto(Guid DemandId, string DemandNo, int FiscalYear, string FiscalYearLabel, DemandStatus Status, decimal Balance, DateOnly? DueDate);

/// What is behind a 1Link reference (a bill inquiry).
public sealed record ReferenceDto(
  string Psid, string PsidFormatted, Guid TaxFileId, string FileNo, string PropertyCode, string? PayerName, decimal AmountDue, DateOnly? DueDate,
  IReadOnlyList<ReferenceYearDto> Years);

public sealed record SmsDto(Guid Id, Guid? TaxFileId, Guid? DemandId, Guid? NoticeId, string ToMobile, string TemplateCode, string Body, DateTime QueuedAt,
  DateTime? SentAt, SmsStatus? DeliveryStatus);

// ---- fees, parties, clearances ----

public sealed record PartyDto(Guid Id, PartyType PartyType, string Name, string? CnicNtn, string? Mobile, string? Address, Guid? OwnerId, Guid? EmployeeId);

public sealed record FeeDto(
  Guid Id, string FeeNo, Guid HeadId, string HeadCode, string HeadName, Guid? TaxFileId, string? FileNo, Guid? PartyId, string? PartyName, FeeSourceType? SourceType,
  Guid? SourceId, string? SourceNo, decimal? BasisQty, string? BasisUnit, decimal Amount, string? ExemptionReason, FeeStatus Status, DateTime? CreatedAt);

public sealed record FeeQuoteDto(string HeadCode, string HeadName, decimal Amount, string Explanation, decimal? Quantity, string? Unit, Guid? RateBandId, string RateSetName);

public sealed record ClearanceDto(
  Guid Id, string ClearanceNo, Guid TaxFileId, string FileNo, ClearancePurpose Purpose, Guid? PropertyRefId, string? PropertyRefNo, decimal OutstandingChecked,
  Guid? FeeId, DateOnly IssuedOn, DateOnly ValidUntil, Guid? IssuedBy, DateTime? ConsumedAt, ClearanceStatus Status);

// ---- reports ----

public sealed record CircleCollectionDto(Guid CircleId, string CircleCode, int Demands, int Paid, decimal Demanded, decimal Recovered, decimal Outstanding);

/// The year at a glance: what was demanded (this year's charges, arrears billed and surcharges), recovered, outstanding,
/// by circle, and the surcharges applied.
public sealed record CollectionSummaryDto(
  int FiscalYear, string FiscalYearLabel, int TaxFiles, int Demands, int Paid, int PartPaid, int Unpaid, int Defaulted, decimal Demanded, decimal Recovered,
  int RecoveryPct, decimal OutstandingAllYears, decimal IntraYearSurcharges, decimal YearEndSurcharges, int PendingNotices,
  IReadOnlyList<CircleCollectionDto> ByCircle);
