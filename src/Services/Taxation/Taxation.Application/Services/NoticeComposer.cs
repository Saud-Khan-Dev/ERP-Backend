using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// What the creator of a notice entered; anything null is pre-filled.
public sealed record NoticeOptions(
  string? PayerName = null,
  string? PayerMobile = null,
  string? RefId = null,
  string? PlotNo = null,
  string? Town = null,
  string? AddressLine = null,
  int? Days = null,
  string? Paragraph = null,
  DateOnly? ReplyBy = null,
  PayVia? PayVia = null,
  bool SendSms = true,
  bool ShowsParagraph = true,
  bool ShowsTable = true,
  IReadOnlyList<TableColumnKey>? Columns = null,
  decimal? Surcharge = null,
  PenaltyRunId? PenaltyRunId = null);

/// Prepares notices (pre-filled, PENDING) and issues them (the table, amount, QR, signing officer and SMS frozen).
public sealed class NoticeComposer(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  CodeIssuer codes,
  CalendarProvider calendars,
  Messenger messenger,
  IHrmDirectory hrm,
  IClock clock,
  ILogger<NoticeComposer> logger)
{
  public async Task<Notice> PrepareAsync(Demand demand, TaxFile file, NoticeStage stage, NoticeOrigin origin, DateOnly issueDate, NoticeOptions? options,
      CancellationToken cancellationToken)
  {
    var o = options ?? new NoticeOptions();
    // a notice goes to whoever the file's dues are addressed to now (after a transfer, the new owner)
    if ((file.Addressee(issueDate) ?? file.Addressee(clock.Today)) is { } addressee)
      demand.Address(addressee);
    var settings = await setup.SettingsAsync(issueDate, cancellationToken);
    var template = await setup.TemplateAsync(stage, cancellationToken);
    var letterhead = await setup.LetterheadAsync(issueDate, cancellationToken);
    var layout = await setup.LayoutAsync(issueDate, cancellationToken);

    var defaults = await DefaultsAsync(file, Text(o.PayerName) ?? demand.PayerName, demand.PayerCnic, Text(o.PayerMobile) ?? demand.PayerMobile, demand.PayerAddress,
      demand.FiscalYear, stage, issueDate, cancellationToken);
    var days = o.Days ?? defaults.Days;
    var replyBy = o.ReplyBy ?? (await calendars.CalendarAsync(settings, issueDate, issueDate.AddDays(days * 2 + 60), cancellationToken)).AddDays(issueDate, days);
    var variables = defaults with
    {
      RefId = Text(o.RefId) ?? defaults.RefId,
      PlotNo = Text(o.PlotNo) ?? defaults.PlotNo,
      Town = Text(o.Town) ?? defaults.Town,
      AddressLine = Text(o.AddressLine) ?? defaults.AddressLine,
      Days = days,
      Paragraph = Text(o.Paragraph) ?? defaults.Paragraph,
      Surcharge = o.Surcharge
    };

    var notice = Notice.Prepare(NoticeId.New(), await codes.NoticeNoAsync(issueDate, cancellationToken),
      demand, file, stage, origin, issueDate, replyBy, template, letterhead, layout, o.PayVia ?? settings.PayViaDefault, variables,
      await ParticularsAsync(demand, layout, cancellationToken), o.PenaltyRunId, o.SendSms);

    var layoutColumns = layout.OrderedColumns.Select(c => new NoticeColumn(c.ColumnKey, c.Heading)).ToList();
    var columns = o.Columns is null ? null : o.Columns.Select(k => layoutColumns.FirstOrDefault(c => c.Key == k) ?? new NoticeColumn(k, NoticeTableLayout.DefaultHeading(k))).ToList();
    notice.SetPrinting(o.ShowsParagraph, o.ShowsTable, columns);
    context.Notices.Add(notice);
    return notice;
  }

  /// The printed fields as they are pre-filled: the payer, "ID" (the Section's old reference, else the file no.), plot no.,
  /// the town's short code, the address line (name and address), the days and paragraph of the stage, the title and the
  /// circle's office.
  public async Task<NoticeVariables> DefaultsAsync(TaxFile file, string payerName, string? cnic, string? mobile, string? address, int fiscalYear, NoticeStage stage,
      DateOnly issueDate, CancellationToken cancellationToken)
  {
    var settings = await setup.SettingsAsync(issueDate, cancellationToken);
    var template = await setup.TemplateAsync(stage, cancellationToken);
    var circle = await setup.CircleAsync(file.CircleId, cancellationToken);
    return new NoticeVariables
    {
      PayerName = payerName,
      PayerCnic = cnic,
      PayerMobile = mobile,
      PayerAddress = address,
      PropertyCode = file.FileNo,
      FiscalYear = FiscalYear.Label(fiscalYear),
      RefId = file.OldReferenceNo ?? file.FileNo,
      PlotNo = file.PlotNo,
      Town = await setup.TownLabelAsync(file, issueDate, cancellationToken),
      AddressLine = string.Join(", ", new[] { payerName, address }.Where(x => !string.IsNullOrWhiteSpace(x))),
      Days = settings.ReplyDays(stage),
      Paragraph = template.ParagraphText,
      Circle = circle.Code,
      Title = template.TitleText,
      OfficeAddress = circle.OfficeAddress,
      OfficeFax = circle.OfficeFax,
      OfficePhone = circle.OfficePhone
    };
  }

  public async Task<NoticeParticulars> ParticularsAsync(Demand demand, NoticeTableLayout layout, CancellationToken cancellationToken)
  {
    var paid = await ledger.PaidByItemAsync([demand], cancellationToken);
    var surcharge = await setup.HeadAsync(HeadCodes.Surcharge, cancellationToken);
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    return NoticeTable.Build(demand, demand.Items(paid, surcharge.Id), heads, layout);
  }

  /// Issues a pending notice: the amount behind the reference today, the table as printed, the QR for that amount, the
  /// holder of the circle's signing post on the notice date, and the stage's SMS.
  public async Task ConfirmAsync(Notice notice, Demand demand, TaxFile file, Guid actor, CancellationToken cancellationToken)
  {
    // a notice of the cycle confirmed after its day is dated the day it goes out, its days to pay counted from then
    var today = clock.Today;
    if (notice.Origin == NoticeOrigin.Cycle && notice.IssueDate < today && !notice.DateEdited)
    {
      var todaySettings = await setup.SettingsAsync(today, cancellationToken);
      var days = notice.Variables.Days;
      notice.RedateTo(today, (await calendars.CalendarAsync(todaySettings, today, today.AddDays(days * 2 + 60), cancellationToken)).AddDays(today, days));
    }

    var layout = notice.TableLayoutId is { } layoutId
      ? await context.TableLayouts.Include(l => l.Rows).ThenInclude(r => r.Sources).Include(l => l.Columns).AsSplitQuery().FirstAsync(l => l.Id == layoutId, cancellationToken)
      : await setup.LayoutAsync(notice.IssueDate, cancellationToken);
    var settings = await setup.SettingsAsync(notice.IssueDate, cancellationToken);
    var particulars = await ParticularsAsync(demand, layout, cancellationToken);
    var qr = notice.PayVia == PayVia.Onelink ? null : PaymentQr.Payload(settings, file.Psid!, demand.Balance, notice.NoticeNo);
    var signer = await SignerAsync(file, notice.IssueDate, cancellationToken);
    // the endorsement number is taken now, so a cancelled draft leaves no gap in the circle's series
    var circle = await setup.CircleAsync(file.CircleId, cancellationToken);
    var endst = await codes.EndstNoAsync(circle.Code, demand.FiscalYear, cancellationToken);

    notice.Confirm(demand, particulars, qr, signer, endst, actor, clock.UtcNow);

    if (notice.SmsSent)
    {
      var values = Messenger.Values(file, demand, notice.Variables.PayerName);
      values["replyBy"] = notice.ReplyBy?.ToString("dd/MM/yyyy") ?? "";
      values["surcharge"] = Rupees.Text(notice.Variables.Surcharge ?? 0);
      var ownerId = file.Payers.FirstOrDefault(p => p.Id == demand.PayerId)?.OwnerId;
      var sms = await messenger.SmsAsync(MessageCodes.For(notice.Stage), file, demand, notice.Id, notice.Variables.PayerMobile, ownerId, values, cancellationToken);
      notice.LinkSms(sms?.Id);
    }
  }

  private readonly Dictionary<(Guid, DateOnly), SigningOfficer> _signers = new();

  /// The officer who signs: whoever holds the circle's Taxation Officer post (its staff row in force on the date) on that
  /// day, read from HRM's staff directory once per post and day in a request. HRM not answering leaves the name to be
  /// signed by hand; the notice is not held up.
  private async Task<SigningOfficer?> SignerAsync(TaxFile file, DateOnly date, CancellationToken cancellationToken)
  {
    var postId = await setup.SigningPostAsync(file.CircleId, date, cancellationToken);
    if (postId is not { } post)
      return null;
    if (_signers.TryGetValue((post, date), out var known))
      return known;
    var signer = new SigningOfficer(post, null, null);
    if (hrm.IsConfigured)
    {
      try
      {
        var holder = await hrm.PostHolderAsync(post, date, cancellationToken);
        signer = new SigningOfficer(post, holder?.EmployeeId, holder?.Name);
      }
      catch (ExternalServiceException ex)
      {
        logger.LogWarning("Signing officer of circle {Circle} not resolved: {Message}", file.CircleId.Value, ex.Message);
        return signer;
      }
    }
    return _signers[(post, date)] = signer;
  }

  private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// What a sync with Property found besides the facts: the category the property type maps to now when it is not the
/// file's (the Section decides - changing a category needs approval), and whether Property deactivated the property.
public sealed record SyncOutcome(bool BasisChanged, TaxCategory? MappedCategory, bool PropertyActive);

/// Opens tax files from Property and refreshes them.
public sealed class TaxFileImporter(IApplicationDbContext context, IPropertyDirectory property, TaxSetup setup, CodeIssuer codes, Ledger ledger, IClock clock)
{
  /// canApprove: the caller may approve (TAX.APPROVE). Opening a file in a category its property type does not map to,
  /// as exempt, or with its own completion-certificate uplift changes what it is charged like an override, so it needs it.
  public async Task<TaxFile> OpenAsync(Guid propertyId, TaxCategory? category, TaxFileDetails details, bool canApprove, CancellationToken cancellationToken)
  {
    var existing = await context.TaxFiles.Where(f => f.PropertyId == propertyId).Select(f => f.FileNo).FirstOrDefaultAsync(cancellationToken);
    if (existing is not null)
      throw new DomainException($"The property already has tax file {existing}.");

    var snapshot = await property.SnapshotAsync(propertyId, cancellationToken);
    var today = clock.Today;
    var town = snapshot.TownId is { } townId ? await setup.CircleTownAsync(townId, today, cancellationToken) : null;
    if (town is null)
      throw new DomainException($"The town of {snapshot.PropertyCode} ({snapshot.TownName ?? "none"}) is not in any circle. Assign it in Configuration → Circles.");

    var mapped = await CategoryAsync(snapshot, cancellationToken);
    var taxCategory = category ?? mapped
      ?? throw new DomainException($"Property type '{snapshot.PropertyTypeCode ?? "none"}' is not mapped to a tax category. Map it in Configuration → Categories, or give the category.");
    if (!canApprove)
    {
      if (taxCategory == TaxCategory.Exempt)
        throw new DomainException("Opening a file as exempt needs approval rights (TAX.APPROVE).");
      if (taxCategory != mapped)
        throw new DomainException($"Property type '{snapshot.PropertyTypeCode ?? "none"}' {(mapped is null ? "is not mapped to a category" : $"maps to {EnumText.Words(mapped.Value)}")}; "
          + "opening the file in another category needs approval rights (TAX.APPROVE).");
      if (details.UpliftPct is not null || details.UpliftRateBasis is not null)
        throw new DomainException("A file's own completion-certificate uplift changes its tax like an override and needs approval rights (TAX.APPROVE). Leave it to the settings.");
    }

    var settings = await setup.SettingsAsync(today, cancellationToken);
    var psid = Numbers.Psid(settings.PsidPrefix, await codes.NextAsync(NumberSequence.Psid, cancellationToken));
    var fileNo = await codes.FileNoAsync(snapshot.TownCode ?? town.TownCode, taxCategory == TaxCategory.Com ? TaxCategory.Com : TaxCategory.Res, cancellationToken);
    var file = TaxFile.Open(TaxFileId.New(), fileNo, psid, snapshot, town.CircleId, taxCategory, details, today, clock.UtcNow);
    context.TaxFiles.Add(file);
    return file;
  }

  /// Refreshes a file from Property; a property moved to another town, or a town moved to another circle, moves the file.
  /// The year's open demands (and drafts) are addressed to the file's addressee afterwards, so notices follow a transfer.
  public async Task<SyncOutcome> SyncAsync(TaxFile file, CancellationToken cancellationToken)
  {
    var snapshot = await property.SnapshotAsync(file.PropertyId, cancellationToken);
    var townId = snapshot.TownId ?? file.TownId;
    var town = await setup.CircleTownAsync(townId, clock.Today, cancellationToken);
    if (town is null && townId != file.TownId)
      throw new DomainException($"{file.PropertyCode} moved to {snapshot.TownName}, which is not in any circle. Assign the town first.");
    // a new town, or the town's move to another circle taking effect
    if (town is not null && (townId != file.TownId || town.CircleId != file.CircleId))
      file.MoveToCircle(town.CircleId, townId);
    var changed = file.Sync(snapshot, clock.Today, clock.UtcNow);
    await ReaddressAsync(file, cancellationToken);
    var mapped = await CategoryAsync(snapshot, cancellationToken);
    return new SyncOutcome(changed, mapped is { } m && m != file.TaxCategory ? m : null, snapshot.IsActive);
  }

  /// The demands still to be paid (and the drafts) take the file's addressee as it is now.
  public async Task ReaddressAsync(TaxFile file, CancellationToken cancellationToken)
  {
    if (file.Addressee(clock.Today) is not { } payer)
      return;
    foreach (var demand in await ledger.DemandsAsync(file.Id, cancellationToken))
      if (demand.IsOpen || demand.Status == DemandStatus.Draft)
        demand.Address(payer);
  }

  private async Task<TaxCategory?> CategoryAsync(PropertySnapshot snapshot, CancellationToken cancellationToken)
  {
    if (snapshot.PropertyTypeId is { } typeId)
    {
      var mapped = await context.CategoryMaps.Where(m => m.PropertyTypeId == typeId).Select(m => (TaxCategory?)m.TaxCategory).FirstOrDefaultAsync(cancellationToken);
      if (mapped is not null)
        return mapped;
    }
    return CategoryMap.Default(snapshot.PropertyTypeCode);
  }
}
