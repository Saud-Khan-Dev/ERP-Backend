// ---- notices ----

public sealed record NoticeListItemDto(
  Guid Id, string NoticeNo, string? EndstNo, Guid DemandId, string DemandNo, Guid TaxFileId, string FileNo, int FiscalYear, NoticeStage Stage, NoticeOrigin Origin,
  NoticeStatus Status, PayVia PayVia, string PayerName, DateOnly IssueDate, DateOnly? ReplyBy, decimal AmountWithinDue, DateTime? ConfirmedAt, DateOnly? ServedOn,
  ServiceMode? ServiceMode, bool SmsSent);

/// The letterhead as printed on a notice. Logos are fetched from /letterheads/{id}/logo/{side}.
public sealed record PrintedLetterheadDto(
  Guid Id, string GovernmentLine, string AuthorityLine, string? Motto, IReadOnlyList<string> Notes, IReadOnlyList<string> CcLines, string? QrText,
  string? QrBankName, bool HasLogoLeft, bool HasLogoRight, bool Watermark);

/// Everything a printed notice shows, in the Section's format: letterhead, office, title, the To block, the paragraph
/// (placeholders filled), the table (with the chosen columns), the payment block (reference and / or QR) and the
/// signature and endorsement blocks.
public sealed record NoticeDto(
  Guid Id, string NoticeNo, string? EndstNo, NoticeStatus Status, NoticeStage Stage, NoticeOrigin Origin, Guid DemandId, string DemandNo, Guid TaxFileId,
  string FileNo, int FiscalYear, string FiscalYearLabel, DateOnly IssueDate, DateOnly? ReplyBy, PrintedLetterheadDto Letterhead, NoticeVariables Variables,
  string ParagraphText, bool ShowsParagraph, bool ShowsTable, IReadOnlyList<NoticeColumn> Columns, NoticeParticulars Particulars, PayVia PayVia,
  string Psid, string PsidFormatted, string? QrPayload, decimal AmountWithinDue, decimal? AmountAfterDue, decimal BalanceAtIssue, string? SigningOfficerName,
  Guid? SigningPostId, Guid? SigningEmployeeId, int TemplateVersion, Guid? PenaltyRunId, IReadOnlyList<NoticeEdit> EditedFields, bool SmsSent,
  Guid? SmsMessageId, Guid? ConfirmedBy, DateTime? ConfirmedAt, Guid? ServedByEmployeeId, DateOnly? ServedOn, ServiceMode? ServiceMode, DateTime? CreatedAt);

public static class NoticeMappings
{
  public static NoticeListItemDto ToListItem(this Notice n, Demand d, string fileNo) => new(
    n.Id.Value, n.NoticeNo, n.EndstNo, n.DemandId.Value, d.DemandNo, d.TaxFileId.Value, fileNo, d.FiscalYear, n.Stage, n.Origin, n.Status, n.PayVia,
    n.Variables.PayerName, n.IssueDate, n.ReplyBy, n.AmountWithinDue, n.ConfirmedAt, n.ServedOn, n.ServiceMode, n.SmsSent);

  public static NoticeDto ToDto(this Notice n, Demand d, string fileNo, NoticeLetterhead l, IReadOnlyList<NoticeColumn> layoutDefaults, string? qrPayloadPreview)
  {
    var v = n.Variables;
    var paragraph = NoticeTable.FillParagraph(v.Paragraph, new Dictionary<string, string>
    {
      ["amount"] = NoticeTable.Amount(n.AmountWithinDue),
      ["days"] = $"{v.Days} days",
      ["fy"] = v.FiscalYear,
      ["surcharge"] = NoticeTable.Amount(v.Surcharge ?? 0),
      ["total"] = NoticeTable.Amount(n.Particulars.Totals.Current + n.Particulars.Totals.Arrears),
      ["reply_by"] = n.ReplyBy?.ToString("dd/MM/yyyy") ?? "",
      ["psid"] = Numbers.FormatPsid(n.Psid),
      ["code"] = v.PropertyCode,
      ["circle"] = v.Circle
    });
    return new NoticeDto(
      n.Id.Value, n.NoticeNo, n.EndstNo, n.Status, n.Stage, n.Origin, n.DemandId.Value, d.DemandNo, d.TaxFileId.Value, fileNo, d.FiscalYear,
      FiscalYear.Label(d.FiscalYear), n.IssueDate, n.ReplyBy,
      new PrintedLetterheadDto(l.Id.Value, l.GovernmentLine, l.AuthorityLine, l.Motto, l.Notes, l.CcLines, l.QrText, l.QrBankName, l.LogoLeftRef is not null,
        l.LogoRightRef is not null, l.Watermark),
      v, paragraph, n.ShowsParagraph, n.ShowsTable, n.Columns ?? layoutDefaults, n.Particulars, n.PayVia, n.Psid, Numbers.FormatPsid(n.Psid),
      n.QrPayload ?? qrPayloadPreview, n.AmountWithinDue, n.AmountAfterDue, n.BalanceAtIssue, n.SigningOfficerName, n.SigningPostId, n.SigningEmployeeId,
      n.TemplateVersion, n.PenaltyRunId?.Value, n.EditedFields ?? [], n.SmsSent, n.SmsMessageId?.Value, n.ConfirmedBy, n.ConfirmedAt, n.ServedByEmployeeId,
      n.ServedOn, n.ServiceMode, n.CreatedAt);
  }
}

// ---- the form for a notice created by hand ----

public sealed record PolicyOptionDto(Guid Id, string Name, PolicyMethod Method, decimal Amount, string Explanation);

/// A head of the year with its normal amount and the special policies the creator may switch it to.
public sealed record HeadOptionDto(
  Guid HeadId, string HeadCode, string HeadName, decimal Current, decimal Normal, string NormalExplanation, LineMode Mode, Guid? PolicyId, bool IsSpecial,
  IReadOnlyList<PolicyOptionDto> Policies);

/// What the "New notice" form needs: whether the year's demand exists (then its amounts are fixed and the notice is
/// issued on it) or not (then the notice creates it from the computation, amounts editable with a reason), the stage
/// that comes next, the heads with their Normal / Special options, the earlier dues already on record, and the
/// printed fields pre-filled.
public sealed record NoticeContextDto(
  Guid TaxFileId, string FileNo, int FiscalYear, string FiscalYearLabel, Guid? DemandId, string? DemandNo, bool CreatesDemand, string? HoldReason,
  string? SuspendedReason, NoticeStage NextStage, IReadOnlyList<HeadOptionDto> Heads, IReadOnlyList<ArrearsDto> Arrears, decimal ArrearsPreview,
  decimal Surcharges, decimal Paid, decimal Payable, NoticeVariables Defaults, PayVia PayVia, IReadOnlyList<TableColumnDto> Columns,
  IReadOnlyList<string> Notes);
