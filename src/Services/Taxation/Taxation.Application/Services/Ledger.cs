using Microsoft.EntityFrameworkCore;

/// What is owed and what was paid, read so that changes made earlier in the same request count (tracked entities and
/// rows added but not saved yet).
public sealed class Ledger(IApplicationDbContext context)
{
  /// The demands of a file, tracked, with their items.
  public async Task<List<Demand>> DemandsAsync(TaxFileId fileId, CancellationToken cancellationToken)
  {
    var loaded = await context.DemandsWithItems().Where(d => d.TaxFileId == fileId).ToListAsync(cancellationToken);
    return context.Demands.WithLocal(loaded, d => d.TaxFileId == fileId).OrderBy(d => d.FiscalYear).ToList();
  }

  /// Opening arrears of the old register not yet billed on a demand.
  public async Task<List<Arrears>> UnbilledImportsAsync(TaxFileId fileId, CancellationToken cancellationToken)
  {
    var loaded = await context.ArrearsRows.Where(a => a.TaxFileId == fileId && a.AddedToDemandId == null).ToListAsync(cancellationToken);
    return context.ArrearsRows.WithLocal(loaded, a => a.TaxFileId == fileId && a.AddedToDemandId == null);
  }

  /// What payments already settled, per demand line and penalty (arrears rows keep their own outstanding).
  public async Task<Dictionary<Guid, decimal>> PaidByItemAsync(IEnumerable<Demand> demands, CancellationToken cancellationToken)
  {
    var list = demands.ToList();
    var lineIds = list.SelectMany(d => d.Lines).Select(l => l.Id).ToList();
    var penaltyIds = list.SelectMany(d => d.Penalties).Select(p => p.Id).ToList();
    var paid = new Dictionary<Guid, decimal>();
    if (lineIds.Count == 0 && penaltyIds.Count == 0)
      return paid;

    var posted = context.Payments.Where(p => p.Status == PaymentStatus.Posted).Select(p => p.Id);
    var loaded = await context.PaymentAllocations
      .Where(a => posted.Contains(a.PaymentId) && ((a.DemandLineId != null && lineIds.Contains(a.DemandLineId)) || (a.PenaltyId != null && penaltyIds.Contains(a.PenaltyId))))
      .ToListAsync(cancellationToken);
    var lineSet = lineIds.Select(i => i.Value).ToHashSet();
    var penaltySet = penaltyIds.Select(i => i.Value).ToHashSet();
    var all = context.PaymentAllocations.WithLocal(loaded, a => lineSet.Contains(a.ItemId) || penaltySet.Contains(a.ItemId));
    foreach (var a in all)
      paid[a.ItemId] = paid.GetValueOrDefault(a.ItemId) + a.Amount;
    return paid;
  }

  /// What is behind the file's reference: the balances of its issued demands, or, before the first demand, the
  /// opening arrears of the old register.
  public async Task<decimal> DuesAsync(TaxFile file, CancellationToken cancellationToken)
  {
    var demands = await DemandsAsync(file.Id, cancellationToken);
    var issued = demands.Where(d => d.IsIssued).ToList();
    var billed = issued.Where(d => d.Status != DemandStatus.Carried).Sum(d => d.Balance);
    var opening = issued.Count == 0 ? (await UnbilledImportsAsync(file.Id, cancellationToken)).Sum(a => a.Outstanding) : 0m;
    return Rupees.Round(billed + opening);
  }

  /// The arrears a draft of the year would carry: the unpaid balances of earlier issued demands, plus the opening
  /// arrears when the file has never been billed.
  public async Task<decimal> ArrearsPreviewAsync(TaxFile file, int fiscalYear, CancellationToken cancellationToken)
  {
    var demands = await DemandsAsync(file.Id, cancellationToken);
    var earlier = demands.Where(d => d.FiscalYear < fiscalYear && d.IsIssued).ToList();
    var carried = earlier.Where(d => d.Status != DemandStatus.Carried).Sum(d => d.Balance);
    var opening = earlier.Count == 0 ? (await UnbilledImportsAsync(file.Id, cancellationToken)).Sum(a => a.Outstanding) : 0m;
    return carried + opening;
  }

  /// Moves the unpaid balances of the file's earlier demands, and its unbilled opening arrears, onto a demand being issued.
  public async Task<IReadOnlyList<Arrears>> CarryIntoAsync(Demand demand, TaxHeadId surchargeHeadId, Guid? actor, DateTime now, CancellationToken cancellationToken)
  {
    var demands = await DemandsAsync(demand.TaxFileId, cancellationToken);
    var earlier = demands.Where(d => d.FiscalYear < demand.FiscalYear && d.IsIssued && d.Status != DemandStatus.Carried).ToList();
    var paid = await PaidByItemAsync(earlier, cancellationToken);
    var rows = new List<Arrears>();
    foreach (var e in earlier)
    {
      var carried = e.CarryInto(demand, paid, surchargeHeadId, actor, now);
      foreach (var row in carried)
        context.ArrearsRows.Add(row);
      rows.AddRange(carried);
    }
    // the old register's opening arrears go on the first demand issued (they can only be entered before it)
    rows.AddRange(await UnbilledImportsAsync(demand.TaxFileId, cancellationToken));
    return rows;
  }
}

/// Runs the year's computation for a file with the configuration in force on 1 July of the year.
public sealed class Assessor(TaxSetup setup)
{
  public async Task<AssessmentContext> ContextAsync(int fiscalYear, CancellationToken cancellationToken)
  {
    var start = FiscalYear.Start(FiscalYear.Valid(fiscalYear));
    return new AssessmentContext(fiscalYear, await setup.RateSetOnAsync(start, cancellationToken), await setup.HeadsAsync(cancellationToken),
      await setup.SettingsAsync(start, cancellationToken), await setup.PoliciesAsync(cancellationToken));
  }

  public static Assessment Assess(AssessmentContext context, TaxFile file)
  {
    var basis = file.CurrentBasis ?? throw new DomainException($"{file.FileNo} has no Property data yet. Sync it from Property.");
    return TaxCalculator.Assess(new AssessmentInput(file, basis, context.FiscalYear, context.Heads, context.RateSet, context.Settings, context.Policies));
  }
}

public sealed record AssessmentContext(int FiscalYear, RateSet RateSet, IReadOnlyList<TaxHead> Heads, TaxSettings Settings, IReadOnlyList<SpecialPolicy> Policies);

/// Numbers that need the database: file numbers by town and category, endorsement numbers by circle and year, and the
/// running sequences.
public sealed class CodeIssuer(IApplicationDbContext context, INumberSource numbers)
{
  public Task<long> NextAsync(NumberSequence sequence, CancellationToken cancellationToken) => numbers.NextAsync(sequence, cancellationToken);

  public async Task<string> FileNoAsync(string townCode, TaxCategory category, CancellationToken cancellationToken)
  {
    var prefix = Numbers.FilePrefix(townCode, category);
    var used = await context.TaxFiles.Where(f => f.FileNo.StartsWith(prefix)).Select(f => f.FileNo).ToListAsync(cancellationToken);
    used.AddRange(context.TaxFiles.Local.Where(f => f.FileNo.StartsWith(prefix)).Select(f => f.FileNo));
    var max = used.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
    return Numbers.FileNo(townCode, category, max + 1);
  }

  public async Task<string> EndstNoAsync(string circleCode, int fiscalYear, CancellationToken cancellationToken)
  {
    var prefix = $"TAX/{circleCode}/{FiscalYear.Label(fiscalYear)}/";
    var used = await context.Notices.Where(n => n.EndstNo != null && n.EndstNo.StartsWith(prefix)).Select(n => n.EndstNo!).ToListAsync(cancellationToken);
    used.AddRange(context.Notices.Local.Where(n => n.EndstNo != null && n.EndstNo.StartsWith(prefix)).Select(n => n.EndstNo!));
    var max = used.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
    return Numbers.EndstNo(circleCode, fiscalYear, max + 1);
  }

  public async Task<string> NoticeNoAsync(DateOnly issueDate, CancellationToken cancellationToken) =>
      Numbers.NoticeNo(issueDate.Year, await numbers.NextAsync(NumberSequence.NoticeRef, cancellationToken));

  public async Task<string> DemandNoAsync(int fiscalYear, CancellationToken cancellationToken) =>
      Numbers.DemandNo(fiscalYear, await numbers.NextAsync(NumberSequence.DemandNo, cancellationToken));
}

/// The working-day calendar for notice periods, with the HRM holidays of the dates involved.
public sealed class CalendarProvider(IHrmDirectory hrm)
{
  public async Task<WorkCalendar> CalendarAsync(TaxSettings settings, DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    if (!settings.CountWorkingDays || !hrm.IsConfigured)
      return new WorkCalendar(settings.CountWorkingDays, new HashSet<DateOnly>());
    return new WorkCalendar(true, await hrm.HolidaysAsync(from, to, cancellationToken));
  }
}

/// Queues the SMS of an event for a payer (when they have a mobile and the template is active), and records the amount
/// behind a reference after every change.
public sealed class Messenger(IApplicationDbContext context, Ledger ledger, IClock clock)
{
  public async Task<SmsMessage?> SmsAsync(string code, TaxFile file, Demand? demand, NoticeId? noticeId, string? mobile, Guid? ownerId,
      IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(mobile))
      return null;
    var template = await context.MessageTemplates.FirstOrDefaultAsync(t => t.Code == code && t.Channel == MessageChannel.Sms && t.IsActive, cancellationToken);
    if (template is null)
      return null;
    var sms = SmsMessage.Queue(SmsMessageId.New(), file.Id, demand?.Id, noticeId, ownerId, mobile, template, template.Fill(values), clock.UtcNow);
    context.SmsMessages.Add(sms);
    return sms;
  }

  /// The usual placeholders of a demand's SMS.
  public static Dictionary<string, string> Values(TaxFile file, Demand demand, string? name = null) => new()
  {
    ["name"] = name ?? demand.PayerName,
    ["code"] = file.FileNo,
    ["fy"] = FiscalYear.Label(demand.FiscalYear),
    ["total"] = Rupees.Text(demand.CurrentAmount + demand.ArrearsBf + demand.PenaltyAmount),
    ["arrears"] = Rupees.Text(demand.ArrearsBf),
    ["balance"] = Rupees.Text(demand.Balance),
    ["psid"] = Numbers.FormatPsid(file.Psid),
    ["due"] = demand.DueDate?.ToString("dd/MM/yyyy") ?? ""
  };

  public async Task<PsidSync> SyncAsync(TaxFile file, PsidSyncReason reason, DemandId? demandId, PenaltyId? penaltyId, DateOnly? dueDate, string? note,
      CancellationToken cancellationToken)
  {
    var dues = await ledger.DuesAsync(file, cancellationToken);
    var loaded = await context.PsidSyncs.Where(s => s.TaxFileId == file.Id).OrderByDescending(s => s.SyncNo).Take(1).ToListAsync(cancellationToken);
    var previous = context.PsidSyncs.WithLocal(loaded, s => s.TaxFileId == file.Id).OrderByDescending(s => s.SyncNo).FirstOrDefault();
    var sync = PsidSync.Record(PsidSyncId.New(), file, previous, reason, dues, demandId, penaltyId, dueDate, note, clock.UtcNow);
    context.PsidSyncs.Add(sync);
    return sync;
  }
}
