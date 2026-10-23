/// What a notice prints under the letterhead and in its paragraph, plus the payer it is addressed to (notice.variables).
/// Pre-filled from the demand, the file and the template; the creator (or the reviewer, with a reason) may change them.
public sealed record NoticeVariables
{
  public string PayerName { get; init; } = "";
  public string? PayerCnic { get; init; }
  public string? PayerMobile { get; init; }
  public string? PayerAddress { get; init; }
  public string PropertyCode { get; init; } = "";
  public string FiscalYear { get; init; } = "";
  /// "ID" on the notice: the Section's old reference, else the property code.
  public string RefId { get; init; } = "";
  public string? PlotNo { get; init; }
  /// The town's short code (NG).
  public string Town { get; init; } = "";
  /// The underlined address line: "Name W/O ..., House, Road, City".
  public string AddressLine { get; init; } = "";
  public int Days { get; init; }
  public string Paragraph { get; init; } = "";
  public decimal? Surcharge { get; init; }
  public string Circle { get; init; } = "";
  /// Frozen with the notice so a reprint matches the original even after the template or the circle changes.
  public string Title { get; init; } = "";
  public string? OfficeAddress { get; init; }
  public string? OfficeFax { get; init; }
  public string? OfficePhone { get; init; }
}

/// One field changed before a notice was issued (notice.edited_fields).
public sealed record NoticeEdit(string Field, string? From, string? To, string Reason, Guid? By, DateTime At);

/// A column printed on a notice: the key and its heading as printed.
public sealed record NoticeColumn(TableColumnKey Key, string Heading);

/// The table of particulars as printed (notice.particulars): one row per layout row, the grand total, the year.
public sealed record NoticeParticulars(int FiscalYear, IReadOnlyList<ParticularRow> Rows, ParticularTotals Totals);

public sealed record ParticularRow(string Label, IReadOnlyList<string> HeadCodes, string? Basis, decimal Arrears, decimal Current, decimal Received, decimal Payable);

public sealed record ParticularTotals(decimal Arrears, decimal Current, decimal Received, decimal Payable);

/// Changes a reviewer may make to a prepared notice before confirming it. Null = unchanged. A new date or number of
/// days to pay moves the pay-by date with it (worked out in working days) unless a pay-by date is given too.
public sealed record NoticeChanges(
  string? PayerName = null,
  string? PayerMobile = null,
  string? RefId = null,
  string? PlotNo = null,
  string? Town = null,
  string? AddressLine = null,
  DateOnly? ReplyBy = null,
  PayVia? PayVia = null,
  bool? ShowsParagraph = null,
  bool? ShowsTable = null,
  IReadOnlyList<TableColumnKey>? Columns = null,
  string? Reason = null,
  DateOnly? IssueDate = null,
  int? Days = null,
  string? Paragraph = null);

/// taxation.notice: a notice on a demand. It is prepared with pre-filled variables (PENDING) and issued only when a user
/// confirms it; issuing freezes the table of particulars, the amount, the QR and the signing officer, so a reprint is
/// identical. The endorsement number is taken when it is issued, so a cancelled draft leaves no gap in the series.
public class Notice : Aggregate<NoticeId>
{
  private List<NoticeEdit> _edits = new();

  public string NoticeNo { get; private set; } = default!;
  public string? EndstNo { get; private set; }
  public NoticeLetterheadId LetterheadId { get; private set; } = default!;
  public bool ShowsParagraph { get; private set; }
  public NoticeTableLayoutId? TableLayoutId { get; private set; }
  public IReadOnlyList<NoticeColumn>? Columns { get; private set; }
  public bool ShowsTable { get; private set; }
  public NoticeParticulars Particulars { get; private set; } = default!;
  public DemandId DemandId { get; private set; } = default!;
  /// The demand's tax file (kept on the notice so the file's history and the town scope reach it directly).
  public TaxFileId TaxFileId { get; private set; } = default!;
  public NoticeStage Stage { get; private set; }
  public NoticeOrigin Origin { get; private set; }
  public PayVia PayVia { get; private set; }
  public string? QrPayload { get; private set; }
  public bool SmsSent { get; private set; }
  public NoticeTemplateId TemplateId { get; private set; } = default!;
  public int TemplateVersion { get; private set; }
  public PenaltyRunId? PenaltyRunId { get; private set; }
  public NoticeVariables Variables { get; private set; } = default!;
  public IReadOnlyList<NoticeEdit>? EditedFields { get => _edits.Count == 0 ? null : _edits; private set => _edits = value?.ToList() ?? new(); }
  public NoticeStatus Status { get; private set; }
  public Guid? ConfirmedBy { get; private set; }
  public DateTime? ConfirmedAt { get; private set; }
  public string Psid { get; private set; } = default!;
  public decimal AmountWithinDue { get; private set; }
  public decimal? AmountAfterDue { get; private set; }
  public SmsMessageId? SmsMessageId { get; private set; }
  public string? PdfDocumentRef { get; private set; }
  public DateOnly IssueDate { get; private set; }
  public DateOnly? ReplyBy { get; private set; }
  public decimal BalanceAtIssue { get; private set; }
  public Guid? SigningPostId { get; private set; }
  public Guid? SigningEmployeeId { get; private set; }
  public string? SigningOfficerName { get; private set; }
  public Guid? ServedByEmployeeId { get; private set; }
  public DateOnly? ServedOn { get; private set; }
  public ServiceMode? ServiceMode { get; private set; }
  public Guid? CancelledBy { get; private set; }
  public DateTime? CancelledAt { get; private set; }

  /// Concurrency token (Postgres xmin): two people confirming the same notice at once - the second is refused.
  public uint Version { get; private set; }

  private Notice() { }

  public bool DateEdited => _edits.Any(e => e.Field == "issueDate");
  public bool ReplyByEdited => _edits.Any(e => e.Field == "replyBy");

  public static Notice Prepare(NoticeId id, string noticeNo, Demand demand, TaxFile file, NoticeStage stage, NoticeOrigin origin, DateOnly issueDate,
      DateOnly replyBy, NoticeTemplate template, NoticeLetterhead letterhead, NoticeTableLayout layout, PayVia payVia, NoticeVariables variables,
      NoticeParticulars particulars, PenaltyRunId? penaltyRunId, bool sendSms)
  {
    if (!demand.IsIssued)
      throw new DomainException($"{demand.DemandNo} is not issued; notices go out on issued demands.");
    if (template.Stage != stage)
      throw new DomainException("The template is for another stage.");
    if (file.Psid is null)
      throw new DomainException($"{file.FileNo} has no 1Link reference.");

    return new Notice
    {
      Id = id,
      NoticeNo = noticeNo,
      LetterheadId = letterhead.Id,
      ShowsParagraph = true,
      TableLayoutId = layout.Id,
      ShowsTable = true,
      Particulars = particulars,
      DemandId = demand.Id,
      TaxFileId = demand.TaxFileId,
      Stage = stage,
      Origin = origin,
      PayVia = payVia,
      SmsSent = sendSms,
      TemplateId = template.Id,
      TemplateVersion = template.VersionNo,
      PenaltyRunId = penaltyRunId,
      Variables = CheckVariables(variables),
      Status = NoticeStatus.Pending,
      Psid = file.Psid,
      AmountWithinDue = demand.Balance,
      IssueDate = issueDate,
      ReplyBy = replyBy < issueDate ? throw new DomainException("The reply date cannot be before the notice date.") : replyBy,
      BalanceAtIssue = demand.Balance
    };
  }

  /// What the creator chose on the form: printed parts and columns. At least one of paragraph and table is printed.
  public void SetPrinting(bool showsParagraph, bool showsTable, IReadOnlyList<NoticeColumn>? columns)
  {
    EnsurePending();
    if (!showsParagraph && !showsTable)
      throw new DomainException("Print the paragraph, the table, or both.");
    if (showsTable && columns is not null)
      NoticeTableLayout.EnsureAmountColumn(columns.Select(c => c.Key));
    ShowsParagraph = showsParagraph;
    ShowsTable = showsTable;
    Columns = columns is null ? null : WithParticular(columns);
  }

  /// Changes made before issue. Every changed value is recorded with the reason; a reason is required when anything printed
  /// changes. movedReplyBy: the pay-by date worked out for a new date or number of days (used when no pay-by is given).
  public void Change(NoticeChanges changes, IReadOnlyList<NoticeColumn> layoutColumns, Guid? actor, DateTime now, DateOnly? movedReplyBy = null)
  {
    EnsurePending();
    var v = Variables;
    var edits = new List<NoticeEdit>();
    string? Edit(string field, string? current, string? next)
    {
      var value = next?.Trim();
      if (string.IsNullOrEmpty(value) || value == current)
        return current;
      edits.Add(new NoticeEdit(field, current, value, changes.Reason ?? "", actor, now));
      return value;
    }

    var days = v.Days;
    if (changes.Days is { } d && d != v.Days)
    {
      if (d is < 1 or > 90)
        throw new DomainException("Days to pay: a whole number from 1 to 90.");
      edits.Add(new NoticeEdit("days", v.Days.ToString(), d.ToString(), changes.Reason ?? "", actor, now));
      days = d;
    }
    var paragraph = v.Paragraph;
    if (changes.Paragraph is { } p && !string.IsNullOrWhiteSpace(p) && p.Trim() != v.Paragraph)
    {
      paragraph = NoticeTemplate.CheckParagraph(p);
      edits.Add(new NoticeEdit("paragraph", v.Paragraph, paragraph, changes.Reason ?? "", actor, now));
    }

    var updated = v with
    {
      PayerName = Edit("payerName", v.PayerName, changes.PayerName)!,
      PayerMobile = Edit("payerMobile", v.PayerMobile, changes.PayerMobile),
      RefId = Edit("refId", v.RefId, changes.RefId)!,
      PlotNo = Edit("plotNo", v.PlotNo, changes.PlotNo),
      Town = Edit("town", v.Town, changes.Town)!,
      AddressLine = Edit("addressLine", v.AddressLine, changes.AddressLine)!,
      Days = days,
      Paragraph = paragraph
    };

    var issueDate = IssueDate;
    if (changes.IssueDate is { } date && date != IssueDate)
    {
      edits.Add(new NoticeEdit("issueDate", IssueDate.ToString("yyyy-MM-dd"), date.ToString("yyyy-MM-dd"), changes.Reason ?? "", actor, now));
      issueDate = date;
    }
    var replyBy = ReplyBy;
    var nextReply = changes.ReplyBy ?? (issueDate != IssueDate || days != v.Days ? movedReplyBy : null);
    if (nextReply is { } reply && reply != ReplyBy)
    {
      if (reply < issueDate)
        throw new DomainException("The pay-by date cannot be before the notice date.");
      edits.Add(new NoticeEdit("replyBy", ReplyBy?.ToString("yyyy-MM-dd"), reply.ToString("yyyy-MM-dd"), changes.Reason ?? "", actor, now));
      replyBy = reply;
    }
    if (replyBy < issueDate)
      throw new DomainException("The pay-by date cannot be before the notice date - give a later pay-by date with the new notice date.");

    if (edits.Count > 0 && string.IsNullOrWhiteSpace(changes.Reason))
      throw new DomainException("Give a reason for the edit.");
    Variables = CheckVariables(updated);
    IssueDate = issueDate;
    ReplyBy = replyBy;
    _edits.AddRange(edits.Select(e => e with { Reason = Guard.RequiredText(changes.Reason, 300, "Reason") }));

    if (changes.PayVia is { } via)
      PayVia = via;
    var columns = changes.Columns is null ? Columns : changes.Columns.Select(k => layoutColumns.FirstOrDefault(c => c.Key == k) ?? new NoticeColumn(k, NoticeTableLayout.DefaultHeading(k))).ToList();
    SetPrinting(changes.ShowsParagraph ?? ShowsParagraph, changes.ShowsTable ?? ShowsTable, columns);
  }

  /// A notice of the cycle confirmed after its day is issued on the day it is confirmed, with its days to pay counted
  /// from then - unless a reviewer set its date (or its pay-by date) on purpose.
  public void RedateTo(DateOnly today, DateOnly replyBy)
  {
    EnsurePending();
    if (Origin != NoticeOrigin.Cycle || IssueDate >= today || DateEdited)
      return;
    IssueDate = today;
    if (!ReplyByEdited)
      ReplyBy = replyBy;
  }

  /// Issues the notice: the amount behind the reference today, the table as printed, the QR, the signing officer and the
  /// endorsement number of its circle and year. A demand with nothing owed gets no notice.
  public void Confirm(Demand demand, NoticeParticulars particulars, string? qrPayload, SigningOfficer? signer, string endstNo, Guid actor, DateTime now)
  {
    EnsurePending();
    if (demand.Id != DemandId)
      throw new DomainException("The notice belongs to another demand.");
    if (demand.Balance <= 0)
      throw new DomainException($"Nothing is owed on {demand.DemandNo} now, so notice {NoticeNo} is not issued. Cancel it.");
    Status = NoticeStatus.Issued;
    EndstNo = Guard.RequiredText(endstNo, 40, "Endorsement no.");
    ConfirmedBy = actor;
    ConfirmedAt = now;
    AmountWithinDue = demand.Balance;
    BalanceAtIssue = demand.Balance;
    Particulars = particulars;
    QrPayload = PayVia == PayVia.Onelink ? null : qrPayload;
    SigningPostId = signer?.PostId;
    SigningEmployeeId = signer?.EmployeeId;
    SigningOfficerName = signer?.Name is { } name ? (name.Length <= 200 ? name : name[..200]) : null;
    demand.MarkStage(Stage);
  }

  public void LinkSms(SmsMessageId? smsId)
  {
    SmsMessageId = smsId;
    SmsSent = smsId is not null;
  }

  public void Cancel(Guid? actor, DateTime now)
  {
    EnsurePending();
    Status = NoticeStatus.Cancelled;
    CancelledBy = actor;
    CancelledAt = now;
  }

  /// Service by the Tax Collector (or refusal).
  public void Serve(Guid? servedByEmployeeId, DateOnly servedOn, ServiceMode mode, DateOnly today)
  {
    if (Status != NoticeStatus.Issued)
      throw new DomainException("Only an issued notice can be served.");
    if (servedOn < IssueDate)
      throw new DomainException("A notice cannot be served before its date.");
    if (servedOn > today)
      throw new DomainException("The service date cannot be in the future.");
    ServedByEmployeeId = servedByEmployeeId;
    ServedOn = servedOn;
    ServiceMode = mode;
  }

  private void EnsurePending()
  {
    if (Status != NoticeStatus.Pending)
      throw new DomainException($"Notice {NoticeNo} is {EnumText.Words(Status)}; only a pending notice can change.");
  }

  private static IReadOnlyList<NoticeColumn> WithParticular(IReadOnlyList<NoticeColumn> columns)
  {
    var list = columns.GroupBy(c => c.Key).Select(g => g.First()).ToList();
    if (list.All(c => c.Key != TableColumnKey.Particular))
      list.Add(new NoticeColumn(TableColumnKey.Particular, NoticeTableLayout.DefaultHeading(TableColumnKey.Particular)));
    return list.OrderBy(c => c.Key).ToList();
  }

  private static NoticeVariables CheckVariables(NoticeVariables v)
  {
    Guard.RequiredText(v.PayerName, 200, "Name");
    Guard.RequiredText(v.AddressLine, 400, "Address");
    Guard.RequiredText(v.RefId, 50, "ID");
    Guard.Text(v.PlotNo, 30, "Plot no.");
    Guard.Text(v.Town, 30, "Town");
    Guard.RequiredText(v.Paragraph, 4000, "Paragraph");
    if (v.Days is < 1 or > 90)
      throw new DomainException("Days to pay: a whole number from 1 to 90.");
    return v;
  }
}

/// The officer who signs a notice: the holder of the circle's Taxation Officer post on the notice date (from HRM).
public sealed record SigningOfficer(Guid? PostId, Guid? EmployeeId, string? Name);
