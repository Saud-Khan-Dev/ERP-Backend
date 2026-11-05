// ---- the taxpayer's own records (/my) ----

/// A tax file the signed-in taxpayer pays on. Role and SharePct: how they hold the property. Town: its short code (NG).
public sealed record MyTaxFileDto(Guid TaxFileId, string FileNo, string PropertyCode, string? PlotNo, string? OldReferenceNo, string? Town, string? TownCode,
  TaxCategory TaxCategory, TaxStatus TaxStatus, PayerRole Role, decimal? SharePct, string? Psid, string? PsidFormatted, decimal Dues, DemandSummaryDto? CurrentDemand);

/// Linked: the account is a taxpayer's (the `own` claim); false shows a "not registered as a taxpayer" page.
public sealed record MyTaxesDto(bool Linked, string? Name, decimal TotalDue, IReadOnlyList<MyTaxFileDto> Files);

/// A notice as the taxpayer received it (only issued notices).
public sealed record MyNoticeDto(Guid Id, string NoticeNo, string? EndstNo, Guid DemandId, int FiscalYear, string FiscalYearLabel, NoticeStage Stage, DateOnly IssueDate,
  DateOnly? ReplyBy, decimal AmountWithinDue, DateOnly? ServedOn);

/// Demands newest first (not drafts or cancelled ones), notices newest first, payments newest first.
public sealed record MyTaxFileDetailDto(MyTaxFileDto File, IReadOnlyList<DemandSummaryDto> Demands, IReadOnlyList<MyNoticeDto> Notices, IReadOnlyList<PaymentDto> Payments);

/// How to pay what is due on a file: the 1Link reference (typed in a banking app's bill payment) and the payment QR for the
/// whole amount (null when nothing is due). The money is paid in the bank's app; it reaches the file when the bank reports it.
public sealed record MyPaymentDetailsDto(Guid TaxFileId, string FileNo, string Psid, string PsidFormatted, decimal Amount, string? PayerName, string MerchantName,
  string? BankName, string? QrPayload);

public sealed record MyMessageDto(Guid Id, string? FileNo, string Body, DateTime QueuedAt, DateTime? SentAt, SmsStatus? Status);
