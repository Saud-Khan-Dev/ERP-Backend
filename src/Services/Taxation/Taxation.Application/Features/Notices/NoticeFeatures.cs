using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed record GetNoticesQueryResult(PaginatedResult<NoticeListItemDto> Notices);
public sealed record GetNoticesQuery(PaginationRequest Pagination, NoticeStatus? Status, NoticeStage? Stage, int? FiscalYear, Guid? CircleId, DateOnly? IssuedOn,
  bool? Served, Guid? TaxFileId, Guid? DemandId, string? Search) : IQuery<Result<GetNoticesQueryResult>>;

public sealed record GetNoticeQueryResult(NoticeDto Notice);
public sealed record GetNoticeQuery(Guid Id) : IQuery<Result<GetNoticeQueryResult>>;

/// Saves changes to a pending notice without issuing it.
public sealed record UpdatePendingNoticeCommand(Guid Id, NoticeChanges Changes) : ICommand<Result<UpdatedResult>>;

/// Issues a pending notice, with any last changes (a reason is needed when anything printed changes).
public sealed record ConfirmNoticeCommand(Guid Id, NoticeChanges? Changes) : ICommand<Result<UpdatedResult>>;

/// Issues every pending notice dated today or earlier, as prepared.
public sealed record ConfirmPendingNoticesCommand(Guid? CircleId, NoticeStage? Stage) : ICommand<Result<ConfirmPendingResult>>;
public sealed record ConfirmPendingResult(int Issued, int SmsQueued);

public sealed record CancelNoticeCommand(Guid Id) : ICommand<Result<UpdatedResult>>;
public sealed record ServeNoticeCommand(Guid Id, Guid? ServedByEmployeeId, DateOnly ServedOn, ServiceMode Mode) : ICommand<Result<UpdatedResult>>;

/// Prepares the next notice of every unpaid demand whose date has come (second, final, show cause, counted in working days
/// from the first notice), for confirmation.
public sealed record EscalateNoticesCommand : ICommand<Result<EscalateResult>>;
public sealed record EscalateResult(int Prepared, IReadOnlyList<NoticeBriefDto> Notices);

public sealed record GetNoticeContextQuery(Guid TaxFileId, int FiscalYear) : IQuery<Result<NoticeContextDto>>;

public sealed record HeadModeInput(Guid HeadId, bool Special, Guid? PolicyId);
public sealed record LineAmountInput(Guid HeadId, decimal Amount);
public sealed record DuesRowInput(int? FiscalYear, string HeadCode, string? Description, decimal Amount);

/// A notice created by hand for one property. When the year's demand exists the notice is issued on it (its amounts are
/// fixed; a head may be switched Normal / Special with a reason); otherwise the notice creates it from the computation
/// (amounts may be changed, with a reason). Earlier years' dues and other charges can be added by year and head.
public sealed record CreateManualNoticeCommand(
  Guid TaxFileId,
  int FiscalYear,
  NoticeStage Stage,
  DateOnly? IssueDate,
  NoticeOptions Options,
  IReadOnlyList<HeadModeInput>? HeadModes,
  IReadOnlyList<LineAmountInput>? LineAmounts,
  IReadOnlyList<DuesRowInput>? Rows,
  string? Reason,
  bool IssueNow) : ICommand<Result<ManualNoticeResult>>;

public sealed record ManualNoticeResult(Guid NoticeId, string NoticeNo, Guid DemandId, string DemandNo, bool CreatedDemand, decimal Added, IReadOnlyList<string> Changes,
  bool Issued, decimal Excess);

public class CreateManualNoticeCommandValidator : AbstractValidator<CreateManualNoticeCommand>
{
  public CreateManualNoticeCommandValidator()
  {
    RuleFor(x => x.TaxFileId).NotEmpty();
    RuleFor(x => x.FiscalYear).InclusiveBetween(FiscalYear.Earliest, FiscalYear.Latest);
    RuleFor(x => x.Options).NotNull();
    RuleFor(x => x.Options.Days).InclusiveBetween(1, 90).When(x => x.Options?.Days is not null);
    RuleFor(x => x.Options.Paragraph).MaximumLength(4000);
    RuleFor(x => x.Options.AddressLine).MaximumLength(400);
    RuleFor(x => x.Reason).MaximumLength(300);
    RuleForEach(x => x.Rows).ChildRules(r =>
    {
      r.RuleFor(x => x.HeadCode).NotEmpty();
      r.RuleFor(x => x.Amount).GreaterThan(0);
      r.RuleFor(x => x.Description).MaximumLength(300);
    });
    RuleForEach(x => x.LineAmounts).ChildRules(r => r.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0));
  }
}

public class ServeNoticeCommandValidator : AbstractValidator<ServeNoticeCommand>
{
  public ServeNoticeCommandValidator() => RuleFor(x => x.ServedOn).NotEmpty();
}

public class NoticeHandlers(
  IApplicationDbContext context,
  TaxSetup setup,
  Ledger ledger,
  Assessor assessor,
  NoticeComposer composer,
  CalendarProvider calendars,
  Messenger messenger,
  CodeIssuer codes,
  ICurrentUser currentUser,
  IClock clock) :
  IQueryHandler<GetNoticesQuery, Result<GetNoticesQueryResult>>,
  IQueryHandler<GetNoticeQuery, Result<GetNoticeQueryResult>>,
  ICommandHandler<UpdatePendingNoticeCommand, Result<UpdatedResult>>,
  ICommandHandler<ConfirmNoticeCommand, Result<UpdatedResult>>,
  ICommandHandler<ConfirmPendingNoticesCommand, Result<ConfirmPendingResult>>,
  ICommandHandler<CancelNoticeCommand, Result<UpdatedResult>>,
  ICommandHandler<ServeNoticeCommand, Result<UpdatedResult>>,
  ICommandHandler<EscalateNoticesCommand, Result<EscalateResult>>,
  IQueryHandler<GetNoticeContextQuery, Result<NoticeContextDto>>,
  ICommandHandler<CreateManualNoticeCommand, Result<ManualNoticeResult>>
{
  private static readonly NoticeStage[] Ladder = [NoticeStage.First, NoticeStage.Second, NoticeStage.Final, NoticeStage.ShowCause];

  // ---- reads ----

  public async Task<Result<GetNoticesQueryResult>> Handle(GetNoticesQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Notices.AsNoTracking();
    if (query.Status is { } status)
      rows = rows.Where(n => n.Status == status);
    if (query.Stage is { } stage)
      rows = rows.Where(n => n.Stage == stage);
    if (query.IssuedOn is { } on)
      rows = rows.Where(n => n.IssueDate == on);
    if (query.Served is { } served)
      rows = served ? rows.Where(n => n.ServedOn != null) : rows.Where(n => n.ServedOn == null && n.Status == NoticeStatus.Issued);
    if (query.DemandId is { } d)
    {
      var demandId = DemandId.Of(d);
      rows = rows.Where(n => n.DemandId == demandId);
    }
    if (query.FiscalYear is not null || query.TaxFileId is not null || query.CircleId is not null)
    {
      var demands = context.Demands.AsQueryable();
      if (query.FiscalYear is { } fy)
        demands = demands.Where(x => x.FiscalYear == fy);
      if (query.TaxFileId is { } f)
      {
        var fileId = TaxFileId.Of(f);
        demands = demands.Where(x => x.TaxFileId == fileId);
      }
      if (query.CircleId is { } c)
      {
        var circleId = TaxCircleId.Of(c);
        var inCircle = context.TaxFiles.Where(x => x.CircleId == circleId).Select(x => x.Id);
        demands = demands.Where(x => inCircle.Contains(x.TaxFileId));
      }
      var demandIds = demands.Select(x => x.Id);
      rows = rows.Where(n => demandIds.Contains(n.DemandId));
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var reference = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
      // the file and the payer through the notice's demand
      var fileMatches = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(s)).Select(f => f.Id);
      var demandMatches = context.Demands.Where(d => d.PayerName.ToUpper().Contains(s) || fileMatches.Contains(d.TaxFileId)).Select(d => d.Id);
      rows = rows.Where(n => n.NoticeNo.ToUpper().Contains(s) || (n.EndstNo != null && n.EndstNo.ToUpper().Contains(s)) || n.Psid.Contains(reference) || demandMatches.Contains(n.DemandId));
    }

    var total = await rows.LongCountAsync(cancellationToken);
    var page = await rows.OrderByDescending(n => n.IssueDate).ThenByDescending(n => n.CreatedAt)
      .Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize).ToListAsync(cancellationToken);
    var demandIdsOnPage = page.Select(n => n.DemandId).Distinct().ToList();
    var demandMap = await context.Demands.AsNoTracking().Where(x => demandIdsOnPage.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    var fileIds = demandMap.Values.Select(x => x.TaxFileId).Distinct().ToList();
    var files = await context.TaxFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var data = page.Select(n => n.ToListItem(demandMap[n.DemandId], files.GetValueOrDefault(demandMap[n.DemandId].TaxFileId) ?? "")).ToList();
    return Result<GetNoticesQueryResult>.Success(new(new PaginatedResult<NoticeListItemDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, data)));
  }

  public async Task<Result<GetNoticeQueryResult>> Handle(GetNoticeQuery query, CancellationToken cancellationToken)
  {
    var notice = await context.LoadNoticeAsync(query.Id, cancellationToken);
    var demand = await context.LoadDemandAsync(notice.DemandId.Value, cancellationToken);
    var file = await context.LoadTaxFileAsync(demand.TaxFileId.Value, cancellationToken);
    var letterhead = await context.Letterheads.AsNoTracking().FirstAsync(l => l.Id == notice.LetterheadId, cancellationToken);
    var layout = notice.TableLayoutId is { } id
      ? await context.TableLayouts.AsNoTracking().Include(l => l.Columns).FirstAsync(l => l.Id == id, cancellationToken)
      : await setup.LayoutAsync(notice.IssueDate, cancellationToken);
    var defaults = layout.OrderedColumns.Where(c => c.PrintedByDefault).Select(c => new NoticeColumn(c.ColumnKey, c.Heading)).ToList();

    // a pending notice shows what it would print today; an issued one shows what was printed
    string? qrPreview = null;
    if (notice.Status == NoticeStatus.Pending && notice.PayVia != PayVia.Onelink)
      qrPreview = PaymentQr.Payload(await setup.SettingsAsync(notice.IssueDate, cancellationToken), file.Psid!, demand.Balance, notice.NoticeNo);
    return Result<GetNoticeQueryResult>.Success(new(notice.ToDto(demand, file.FileNo, letterhead, defaults, qrPreview)));
  }

  // ---- review and issue ----

  public async Task<Result<UpdatedResult>> Handle(UpdatePendingNoticeCommand command, CancellationToken cancellationToken)
  {
    var notice = await context.LoadNoticeAsync(command.Id, cancellationToken);
    notice.Change(command.Changes, await LayoutColumnsAsync(notice, cancellationToken), currentUser.UserId, clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ConfirmNoticeCommand command, CancellationToken cancellationToken)
  {
    var notice = await context.LoadNoticeAsync(command.Id, cancellationToken);
    var demand = await context.LoadDemandAsync(notice.DemandId.Value, cancellationToken);
    var file = await context.LoadTaxFileAsync(demand.TaxFileId.Value, cancellationToken);
    var actor = Guard.Actor(currentUser.UserId, "issue a notice");
    if (command.Changes is { } changes)
      notice.Change(changes, await LayoutColumnsAsync(notice, cancellationToken), actor, clock.UtcNow);
    if (notice.IssueDate > clock.Today)
      return Result<UpdatedResult>.Failure($"Notice {notice.NoticeNo} is dated {notice.IssueDate:yyyy-MM-dd}; it is issued on or after that date.");
    if (file.HoldOn(clock.Today) is { } hold)
      return Result<UpdatedResult>.Failure($"{file.FileNo} is on litigation hold since {hold.StartedOn:yyyy-MM-dd}; no notice is issued while it is open.");
    await composer.ConfirmAsync(notice, demand, file, actor, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<ConfirmPendingResult>> Handle(ConfirmPendingNoticesCommand command, CancellationToken cancellationToken)
  {
    var actor = Guard.Actor(currentUser.UserId, "issue notices");
    var today = clock.Today;
    var pending = await context.Notices.Where(n => n.Status == NoticeStatus.Pending && n.IssueDate <= today && (command.Stage == null || n.Stage == command.Stage))
      .OrderBy(n => n.IssueDate).ToListAsync(cancellationToken);
    var issued = 0;
    var sms = 0;
    foreach (var notice in pending)
    {
      var demand = await context.LoadDemandAsync(notice.DemandId.Value, cancellationToken);
      var file = await context.LoadTaxFileAsync(demand.TaxFileId.Value, cancellationToken);
      if (command.CircleId is { } circle && file.CircleId.Value != circle)
        continue;
      if (file.HoldOn(today) is not null)
        continue;
      await composer.ConfirmAsync(notice, demand, file, actor, cancellationToken);
      issued++;
      if (notice.SmsMessageId is not null)
        sms++;
    }
    await context.SaveChangesAsync(cancellationToken);
    return Result<ConfirmPendingResult>.Success(new(issued, sms));
  }

  public async Task<Result<UpdatedResult>> Handle(CancelNoticeCommand command, CancellationToken cancellationToken)
  {
    var notice = await context.LoadNoticeAsync(command.Id, cancellationToken);
    notice.Cancel();
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  public async Task<Result<UpdatedResult>> Handle(ServeNoticeCommand command, CancellationToken cancellationToken)
  {
    var notice = await context.LoadNoticeAsync(command.Id, cancellationToken);
    notice.Serve(command.ServedByEmployeeId, command.ServedOn, command.Mode, clock.Today);
    await context.SaveChangesAsync(cancellationToken);
    return CommandResults.Updated();
  }

  // ---- escalation ----

  public async Task<Result<EscalateResult>> Handle(EscalateNoticesCommand command, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var demands = await context.DemandsWithItems()
      .Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid) && d.Balance > 0 && d.IssuedOn != null)
      .ToListAsync(cancellationToken);
    demands = demands.Where(d => today <= FiscalYear.End(d.FiscalYear)).ToList();
    var prepared = new List<Notice>();
    if (demands.Count > 0)
    {
      var ids = demands.Select(d => d.Id).ToList();
      var notices = await context.Notices.AsNoTracking().Where(n => ids.Contains(n.DemandId) && n.Status != NoticeStatus.Cancelled).ToListAsync(cancellationToken);
      var fileIds = demands.Select(d => d.TaxFileId).Distinct().ToList();
      var files = (await context.TaxFilesWithDetails().Where(f => fileIds.Contains(f.Id)).ToListAsync(cancellationToken)).ToDictionary(f => f.Id);
      var settings = await setup.SettingsAsync(today, cancellationToken);
      var calendar = await calendars.CalendarAsync(settings, demands.Min(d => d.IssuedOn!.Value), today.AddDays(1), cancellationToken);
      var steps = new[] { (NoticeStage.Second, settings.NoticeSecondDays), (NoticeStage.Final, settings.NoticeFinalDays), (NoticeStage.ShowCause, settings.NoticeShowCauseDays) };

      foreach (var demand in demands)
      {
        var mine = notices.Where(n => n.DemandId == demand.Id).ToList();
        if (mine.Any(n => n.Status == NoticeStatus.Pending) || files[demand.TaxFileId].HoldOn(today) is not null)
          continue;
        foreach (var (stage, days) in steps)
        {
          if (mine.Any(n => n.Stage == stage))
            continue;
          var due = calendar.AddDays(demand.IssuedOn!.Value, days);
          if (due <= today)
            prepared.Add(await composer.PrepareAsync(demand, files[demand.TaxFileId], stage, NoticeOrigin.Cycle, due, null, cancellationToken));
          break;
        }
      }
    }
    await context.SaveChangesAsync(cancellationToken);
    return Result<EscalateResult>.Success(new(prepared.Count, prepared.Select(n => n.ToBrief()).ToList()));
  }

  // ---- notice created by hand ----

  public async Task<Result<NoticeContextDto>> Handle(GetNoticeContextQuery query, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(query.TaxFileId, cancellationToken);
    var fy = FiscalYear.Valid(query.FiscalYear);
    var today = clock.Today;
    var demand = await IssuedDemandAsync(file, fy, cancellationToken);
    var notes = new List<string>();

    Assessment? fresh = null;
    AssessmentContext? ctx = null;
    try
    {
      ctx = await assessor.ContextAsync(fy, cancellationToken);
      fresh = Assessor.Assess(ctx, file);
    }
    catch (DomainException ex)
    {
      notes.Add(DomainMessages.Text(ex));
    }
    if (fresh is not null)
      notes.AddRange(fresh.Notes);

    if (demand is null && await CarriedAsync(file, fy, cancellationToken) is { } carriedNote)
      notes.Insert(0, carriedNote);
    List<NoticeStage> issuedStages = demand is null ? [] : await context.Notices.Where(n => n.DemandId == demand.Id && n.Status != NoticeStatus.Cancelled).Select(n => n.Stage).ToListAsync(cancellationToken);
    var next = demand is null || demand.Balance <= 0 ? NoticeStage.First : Ladder.FirstOrDefault(s => !issuedStages.Contains(s), NoticeStage.ShowCause);

    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var options = new List<HeadOptionDto>();
    if (demand is not null)
    {
      foreach (var line in demand.Lines)
      {
        var normal = fresh?.Lines.FirstOrDefault(l => l.HeadId == line.HeadId);
        options.Add(HeadOption(heads[line.HeadId], line.Amount, line.NormalAmount ?? normal?.NormalAmount ?? line.Amount, normal?.NormalExplanation ?? line.Explanation ?? "",
          line.LineMode, line.PolicyId, file, fy, ctx));
      }
    }
    else if (fresh is { Suspended: false })
    {
      foreach (var line in fresh.Lines)
        options.Add(HeadOption(heads[line.HeadId], line.Amount, line.NormalAmount, line.NormalExplanation, line.Mode, line.PolicyId, file, fy, ctx));
    }

    var payer = file.Addressee(today);
    var defaults = await composer.DefaultsAsync(file, demand?.PayerName ?? payer?.Name ?? "", demand?.PayerCnic ?? payer?.CnicNtn, demand?.PayerMobile ?? payer?.Mobile,
      demand?.PayerAddress ?? payer?.PostalAddress, fy, next, today, cancellationToken);
    var arrears = demand?.ArrearsRows.Select(a => a.ToDto(heads)).ToList() ?? [];
    var preview = demand is null ? await ledger.ArrearsPreviewAsync(file, fy, cancellationToken) : 0m;
    var layout = await setup.LayoutAsync(today, cancellationToken);
    var current = demand?.CurrentAmount ?? (fresh is { Suspended: false } ? fresh.Total : 0m);
    var payable = demand?.Balance ?? current + preview;

    return Result<NoticeContextDto>.Success(new NoticeContextDto(
      file.Id.Value, file.FileNo, fy, FiscalYear.Label(fy), demand?.Id.Value, demand?.DemandNo, demand is null,
      file.HoldOn(today) is { } h ? $"Litigation hold since {h.StartedOn:yyyy-MM-dd}: {h.Reason}" : null, demand is null ? fresh?.SuspendedReason : null,
      next, options, arrears, preview, demand?.PenaltyAmount ?? 0, demand?.PaidAmount ?? 0, payable, defaults,
      (await setup.SettingsAsync(today, cancellationToken)).PayViaDefault, layout.ToDto().Columns, notes));
  }

  private static HeadOptionDto HeadOption(TaxHead head, decimal current, decimal normal, string normalExplanation, LineMode mode, SpecialPolicyId? policyId, TaxFile file,
      int fy, AssessmentContext? ctx)
  {
    List<PolicyOptionDto> policies = ctx is null ? [] : ctx.Policies.Where(p => p.AvailableFor(head.Id, file.TaxCategory, fy)).Select(p =>
    {
      var (amount, text) = TaxCalculator.PolicyAmount(p, head, file, fy, normal, ctx.RateSet, ctx.Settings);
      return new PolicyOptionDto(p.Id.Value, p.Name, p.Method, amount, text);
    }).ToList();
    return new HeadOptionDto(head.Id.Value, head.Code, head.Name, current, normal, normalExplanation, mode, policyId?.Value,
      mode is LineMode.Policy or LineMode.Special, policies);
  }

  public async Task<Result<ManualNoticeResult>> Handle(CreateManualNoticeCommand command, CancellationToken cancellationToken)
  {
    var file = await context.LoadTaxFileAsync(command.TaxFileId, cancellationToken);
    var fy = FiscalYear.Valid(command.FiscalYear);
    var today = clock.Today;
    var issueDate = command.IssueDate ?? today;
    var actor = Guard.Actor(currentUser.UserId, "create a notice");
    var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

    if (issueDate > today)
      return Result<ManualNoticeResult>.Failure("A notice cannot be dated in the future.");
    if (file.HoldOn(issueDate) is { } hold)
      return Result<ManualNoticeResult>.Failure($"Litigation hold since {hold.StartedOn:yyyy-MM-dd} ({hold.Reason}): no tax or fee while it is open.");
    await setup.TemplateAsync(command.Stage, cancellationToken);

    // the rows of earlier dues and other charges, checked before anything changes
    var rows = new List<(DuesRowInput Row, TaxHead Head)>();
    foreach (var row in command.Rows ?? [])
    {
      var head = await setup.HeadAsync(row.HeadCode.Trim().ToUpperInvariant(), cancellationToken);
      if (!head.IsActive)
        return Result<ManualNoticeResult>.Failure($"{head.Name} is inactive.");
      if (row.FiscalYear > fy)
        return Result<ManualNoticeResult>.Failure($"A row for FY {FiscalYear.Label(row.FiscalYear.Value)} is after the notice year.");
      if (head.Code == HeadCodes.Misc && string.IsNullOrWhiteSpace(row.Description))
        return Result<ManualNoticeResult>.Failure("Describe each miscellaneous charge (what it is for).");
      rows.Add((row, head));
    }

    var demand = await IssuedDemandAsync(file, fy, cancellationToken);
    var created = demand is null;
    if (created && await CarriedAsync(file, fy, cancellationToken) is { } carried)
      return Result<ManualNoticeResult>.Failure(carried);

    // the computation is needed to create the demand or to switch a head; an issued demand alone is not recomputed
    AssessmentContext? ctx = null;
    Assessment? fresh = null;
    if (created || command.HeadModes is { Count: > 0 })
    {
      ctx = await assessor.ContextAsync(fy, cancellationToken);
      fresh = Assessor.Assess(ctx, file);
      if (created && fresh.Suspended)
        return Result<ManualNoticeResult>.Failure(fresh.SuspendedReason!);
    }

    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var changes = new List<string>();
    var lines = fresh?.Lines.ToList() ?? [];

    // Normal / Special per head
    foreach (var mode in command.HeadModes ?? [])
    {
      var headId = TaxHeadId.Of(mode.HeadId);
      var head = heads.GetValueOrDefault(headId) ?? throw new TaxHeadNotFoundException($"Tax head {mode.HeadId} was not found.");
      var normal = fresh!.Lines.FirstOrDefault(l => l.HeadId == headId);
      if (created)
      {
        var index = lines.FindIndex(l => l.HeadId == headId);
        if (index < 0)
          return Result<ManualNoticeResult>.Failure($"{head.Name} is not charged this year.");
        var line = lines[index];
        if (mode.Special && mode.PolicyId is { } pid && pid != line.PolicyId?.Value)
        {
          var policy = Policy(ctx!, pid, head, file, fy);
          var (amount, text) = TaxCalculator.PolicyAmount(policy, head, file, fy, line.NormalAmount, ctx!.RateSet, ctx.Settings);
          lines[index] = line with { BaseAmount = amount, UpliftAmount = 0, IncreaseAmount = 0, OverrideAmount = 0, Amount = amount, Mode = LineMode.Policy, PolicyId = policy.Id, OverrideId = null, Explanation = text };
          changes.Add($"{head.Name}: special policy \"{policy.Name}\" {Rupees.Text(line.Amount)} → {Rupees.Text(amount)}");
        }
        else if (!mode.Special && line.Mode is LineMode.Policy or LineMode.Special)
        {
          lines[index] = line with { BaseAmount = line.NormalBase, UpliftAmount = line.NormalUplift, IncreaseAmount = line.NormalIncrease, OverrideAmount = 0, Amount = line.NormalAmount,
            Mode = LineMode.Normal, PolicyId = null, OverrideId = null, Explanation = line.NormalExplanation };
          changes.Add($"{head.Name}: normal tax {Rupees.Text(line.Amount)} → {Rupees.Text(line.NormalAmount)}");
        }
      }
      else
      {
        var line = demand!.Line(headId);
        if (mode.Special && mode.PolicyId is { } pid && pid != line.PolicyId?.Value)
        {
          var policy = Policy(ctx!, pid, head, file, fy);
          var (amount, text) = TaxCalculator.PolicyAmount(policy, head, file, fy, line.NormalAmount ?? normal?.NormalAmount ?? line.Amount, ctx!.RateSet, ctx.Settings);
          changes.Add($"{head.Name}: special policy \"{policy.Name}\" {Rupees.Text(line.Amount)} → {Rupees.Text(amount)}");
          line.ToPolicy(policy.Id, amount, text);
        }
        else if (!mode.Special && line.LineMode is LineMode.Policy or LineMode.Special or LineMode.Manual)
        {
          if (normal is null)
            return Result<ManualNoticeResult>.Failure($"{head.Name} has no normal amount this year.");
          changes.Add($"{head.Name}: normal tax {Rupees.Text(line.Amount)} → {Rupees.Text(normal.NormalAmount)}");
          line.ToNormal(normal.NormalBase, normal.NormalIncrease, normal.NormalUplift, normal.NormalExplanation);
        }
      }
    }

    // amounts typed in (only when the notice creates the demand)
    if (command.LineAmounts is { Count: > 0 } amounts)
    {
      if (!created)
        return Result<ManualNoticeResult>.Failure("The year's demand is issued; its amounts are changed by switching a head Normal / Special, not typed in.");
      foreach (var entry in amounts)
      {
        var index = lines.FindIndex(l => l.HeadId.Value == entry.HeadId);
        if (index < 0)
          return Result<ManualNoticeResult>.Failure("An amount was given for a head that is not charged this year.");
        var line = lines[index];
        var value = Rupees.Round(entry.Amount);
        if (value == line.Amount)
          continue;
        changes.Add($"{line.HeadName} {Rupees.Text(line.Amount)} → {Rupees.Text(value)} entered");
        lines[index] = line with { OverrideAmount = line.OverrideAmount + value - line.Amount, Amount = value, Mode = LineMode.Manual };
      }
    }

    if ((rows.Count > 0 || changes.Count > 0) && reason is null)
      return Result<ManualNoticeResult>.Failure("Give the reason for the amounts you entered or changed (kept in the audit trail).");

    if (created)
    {
      foreach (var draft in await context.DemandsWithItems().Where(d => d.TaxFileId == file.Id && d.FiscalYear == fy && d.Status == DemandStatus.Draft).ToListAsync(cancellationToken))
        context.Demands.Remove(draft);
      var payer = file.Addressee(issueDate) ?? throw new DomainException("Property has no owner, lessee, tenant or allottee for it. Record one there and sync the file.");
      demand = Demand.Draft(DemandId.New(), await codes.DemandNoAsync(fy, cancellationToken), null, file, file.CurrentBasis!, payer, fy, lines, 0);
      context.Demands.Add(demand);
      var surcharge = await setup.HeadAsync(HeadCodes.Surcharge, cancellationToken);
      var billed = await ledger.CarryIntoAsync(demand, surcharge.Id, actor, clock.UtcNow, cancellationToken);
      var settings = await setup.SettingsAsync(issueDate, cancellationToken);
      demand.Issue(issueDate, settings.DueDate(fy), billed);
    }
    else if (changes.Count > 0)
    {
      demand!.AfterChange(today);
    }

    foreach (var (row, head) in rows)
      context.ArrearsRows.Add(demand!.AddManualArrears(head, row.FiscalYear, row.Amount, row.Description, reason!, actor, clock.UtcNow));
    var added = rows.Sum(r => Rupees.Round(r.Row.Amount));

    file.FillPlotNo(command.Options.PlotNo);
    var notice = await composer.PrepareAsync(demand!, file, command.Stage, NoticeOrigin.Manual, issueDate, command.Options, cancellationToken);

    if (created || added > 0 || changes.Count > 0)
    {
      var note = created
        ? $"FY {FiscalYear.Label(fy)} demand {demand!.DemandNo} created with notice {notice.NoticeNo}"
        : string.Join(", ", new[] { added > 0 ? $"{Rupees.Text(added)} added" : null, changes.Count > 0 ? $"{changes.Count} head(s) changed" : null }.Where(x => x is not null))
          + $" with notice {notice.NoticeNo}";
      await messenger.SyncAsync(file, created ? PsidSyncReason.AnnualDemand : PsidSyncReason.Correction, demand!.Id, null, demand.DueDate, note, cancellationToken);
    }

    if (command.IssueNow)
      await composer.ConfirmAsync(notice, demand!, file, actor, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
    return Result<ManualNoticeResult>.Success(new(notice.Id.Value, notice.NoticeNo, demand!.Id.Value, demand.DemandNo, created, added, changes, command.IssueNow, demand.Excess));
  }

  private static SpecialPolicy Policy(AssessmentContext ctx, Guid id, TaxHead head, TaxFile file, int fy)
  {
    var policy = ctx.Policies.FirstOrDefault(p => p.Id.Value == id) ?? throw new SpecialPolicyNotFoundException($"Special policy {id} was not found.");
    if (!policy.IsActive)
      throw new DomainException($"Choose an active special policy for {head.Name}.");
    if (policy.HeadId != head.Id)
      throw new DomainException($"\"{policy.Name}\" is not a policy for {head.Name}.");
    if (!policy.AvailableFor(head.Id, file.TaxCategory, fy))
      throw new DomainException($"\"{policy.Name}\" is not in force for this property in FY {FiscalYear.Label(fy)}.");
    return policy;
  }

  /// Why no notice can be issued on a year whose balance has moved on to a later year's demand (null when it has not).
  private async Task<string?> CarriedAsync(TaxFile file, int fiscalYear, CancellationToken cancellationToken)
  {
    var carried = await context.Demands.AsNoTracking()
      .FirstOrDefaultAsync(d => d.TaxFileId == file.Id && d.FiscalYear == fiscalYear && d.Status == DemandStatus.Carried, cancellationToken);
    if (carried is null)
      return null;
    var into = carried.CarriedToDemandId is { } id ? await context.Demands.AsNoTracking().Where(d => d.Id == id).Select(d => d.FiscalYear).FirstOrDefaultAsync(cancellationToken) : 0;
    return $"The FY {FiscalYear.Label(fiscalYear)} balance was carried into the FY {FiscalYear.Label(into)} demand as arrears. Issue the notice on FY {FiscalYear.Label(into)}.";
  }

  /// The issued demand of a year that still holds its balance (not a draft, not carried, not cancelled).
  private async Task<Demand?> IssuedDemandAsync(TaxFile file, int fiscalYear, CancellationToken cancellationToken) =>
      await context.DemandsWithItems().FirstOrDefaultAsync(d => d.TaxFileId == file.Id && d.FiscalYear == fiscalYear
        && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled && d.Status != DemandStatus.Carried, cancellationToken);

  private async Task<IReadOnlyList<NoticeColumn>> LayoutColumnsAsync(Notice notice, CancellationToken cancellationToken)
  {
    var layout = notice.TableLayoutId is { } id
      ? await context.TableLayouts.AsNoTracking().Include(l => l.Columns).FirstAsync(l => l.Id == id, cancellationToken)
      : await setup.LayoutAsync(notice.IssueDate, cancellationToken);
    return layout.OrderedColumns.Select(c => new NoticeColumn(c.ColumnKey, c.Heading)).ToList();
  }
}
