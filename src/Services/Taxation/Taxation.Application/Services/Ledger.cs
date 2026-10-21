using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    // allocations of a payment reversed in this request no longer count
    var reversed = context.Payments.Local.Where(p => p.Status == PaymentStatus.Reversed).Select(p => p.Id).ToHashSet();
    var all = context.PaymentAllocations.WithLocal(loaded, a => lineSet.Contains(a.ItemId) || penaltySet.Contains(a.ItemId)).Where(a => !reversed.Contains(a.PaymentId));
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

  /// The fees charged on a file and not paid yet (they too stand in the way of a clearance).
  public async Task<decimal> FeesDueAsync(TaxFileId fileId, CancellationToken cancellationToken) =>
      await context.Fees.Where(f => f.TaxFileId == fileId && f.Status == FeeStatus.Due).SumAsync(f => (decimal?)f.Amount, cancellationToken) ?? 0m;

  /// What was paid beyond the charges on the file's demands (a correction lowered them after payment) and not carried
  /// on yet: the credit the next demand takes.
  public async Task<decimal> CreditAsync(TaxFileId fileId, CancellationToken cancellationToken) =>
      (await DemandsAsync(fileId, cancellationToken)).Sum(d => d.Excess);

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

  /// Moves the unpaid balances of the file's earlier demands (of years that are over), and its unbilled opening arrears,
  /// onto a demand being issued.
  public async Task<IReadOnlyList<Arrears>> CarryIntoAsync(Demand demand, TaxHeadId surchargeHeadId, Guid? actor, DateTime now, DateOnly today,
      CancellationToken cancellationToken)
  {
    var demands = await DemandsAsync(demand.TaxFileId, cancellationToken);
    var earlier = demands.Where(d => d.FiscalYear < demand.FiscalYear && d.IsIssued && d.Status != DemandStatus.Carried).ToList();
    var paid = await PaidByItemAsync(earlier, cancellationToken);
    var rows = new List<Arrears>();
    foreach (var e in earlier)
    {
      var carried = e.CarryInto(demand, paid, surchargeHeadId, actor, now, today);
      foreach (var row in carried)
        context.ArrearsRows.Add(row);
      rows.AddRange(carried);
    }
    // the old register's opening arrears go on the first demand issued (they can only be entered before it)
    rows.AddRange(await UnbilledImportsAsync(demand.TaxFileId, cancellationToken));
    return rows;
  }

  /// Settles money received against the file's reference: the open demands oldest year first, item by item; before the
  /// first demand, the opening arrears of the old register (oldest year first). Returns the demands it reached.
  public async Task<IReadOnlyList<Demand>> SettleAsync(TaxFile file, Payment payment, TaxHeadId surchargeHeadId, DateOnly today, CancellationToken cancellationToken)
  {
    var demands = (await DemandsAsync(file.Id, cancellationToken)).Where(d => d.IsOpen).OrderBy(d => d.FiscalYear).ToList();
    var left = payment.Amount;
    var touched = new List<Demand>();
    if (demands.Count > 0)
    {
      var paid = await PaidByItemAsync(demands, cancellationToken);
      foreach (var demand in demands)
      {
        if (left <= 0)
          break;
        var settled = demand.Settle(left, paid, surchargeHeadId, today);
        payment.Allocate(settled);
        left -= settled.Sum(s => s.Amount);
        touched.Add(demand);
      }
    }
    else
    {
      var settled = new List<ItemSettlement>();
      foreach (var row in (await UnbilledImportsAsync(file.Id, cancellationToken)).Where(a => a.Outstanding > 0).OrderBy(a => a.FiscalYear ?? 0).ThenBy(a => a.EnteredAt))
      {
        if (left <= 0)
          break;
        var take = Math.Min(left, row.Outstanding);
        row.Settle(take);
        settled.Add(new ItemSettlement(DemandItemKind.Arrears, row.Id.Value, row.HeadId, take));
        left -= take;
      }
      payment.Allocate(settled);
    }
    if (left > 0)
      throw new DomainException($"{Rupees.Text(left)} of the payment found nothing to settle.");
    return touched;
  }

  /// A credit (paid beyond the charges after a correction) moves onto the file's open demands, oldest year first: the
  /// over-paid allocations shrink and the open demand's items get their own. Returns the amount moved.
  public async Task<decimal> MoveCreditsAsync(TaxFileId fileId, TaxHeadId surchargeHeadId, DateOnly today, CancellationToken cancellationToken)
  {
    var demands = await DemandsAsync(fileId, cancellationToken);
    var donors = demands.Where(d => d.Excess > 0).OrderBy(d => d.FiscalYear).ToList();
    var takers = demands.Where(d => d.IsOpen).OrderBy(d => d.FiscalYear).ToList();
    if (donors.Count == 0 || takers.Count == 0)
      return 0m;

    var paid = await PaidByItemAsync(demands, cancellationToken);
    var moved = 0m;
    foreach (var donor in donors)
    {
      var lineIds = donor.Lines.Select(l => l.Id).ToList();
      var penaltyIds = donor.Penalties.Select(p => p.Id).ToList();
      var payments = await context.Payments.Include(p => p.Allocations)
        .Where(p => p.Status == PaymentStatus.Posted && p.Allocations.Any(a => (a.DemandLineId != null && lineIds.Contains(a.DemandLineId))
          || (a.PenaltyId != null && penaltyIds.Contains(a.PenaltyId))))
        .ToListAsync(cancellationToken);
      foreach (var item in donor.Items(paid, surchargeHeadId).Where(i => i.Kind != DemandItemKind.Arrears && i.Paid > i.Amount))
      {
        var over = item.Paid - item.Amount;
        foreach (var payment in payments.OrderByDescending(p => p.PaidAt))
        foreach (var allocation in payment.Allocations.Where(a => a.ItemId == item.ItemId).ToList())
        {
          while (over > 0 && allocation.Amount > 0 && takers.FirstOrDefault(t => t.IsOpen) is { } taker)
          {
            var settled = taker.Settle(Math.Min(over, allocation.Amount), paid, surchargeHeadId, today);
            var amount = settled.Sum(x => x.Amount);
            if (amount <= 0)
              break;
            payment.MoveCredit(allocation.Id, amount, settled);
            donor.ReleasePaid(amount, today);
            foreach (var x in settled.Where(x => x.Kind != DemandItemKind.Arrears))
              paid[x.ItemId] = paid.GetValueOrDefault(x.ItemId) + x.Amount;
            paid[item.ItemId] = paid.GetValueOrDefault(item.ItemId) - amount;
            over -= amount;
            moved += amount;
          }
        }
      }
    }
    return moved;
  }

  /// Takes a payment back: every item it paid owes that again. An item of a year whose balance was carried on is billed
  /// again on the demand that holds the file's balance now (as an earlier year's due); a fee it paid is due again.
  public async Task ReverseAsync(Payment payment, string reason, Guid actor, DateTime now, DateOnly today, CancellationToken cancellationToken)
  {
    var demands = payment.TaxFileId is { } fileId ? await DemandsAsync(fileId, cancellationToken) : [];
    Demand Holder(Demand carried)
    {
      var d = carried;
      for (var hops = 0; d.Status == DemandStatus.Carried && d.CarriedToDemandId is { } next && hops < 100; hops++)
        d = demands.First(x => x.Id == next);
      return d;
    }
    void BillOnHolder(Demand carried, TaxHeadId headId, int? fiscalYear, decimal amount)
    {
      carried.ReleasePaidIntoCarried(amount);
      context.ArrearsRows.Add(Holder(carried).BillAgain(headId, fiscalYear, amount, carried.Id, actor, now));
    }

    foreach (var a in payment.Allocations.ToList())
    {
      if (a.FeeId is { } feeId)
      {
        var fee = await context.Fees.FirstAsync(f => f.Id == feeId, cancellationToken);
        fee.Unpay();
        continue;
      }
      if (a.ArrearsId is { } arrearsId)
      {
        var holderOf = demands.FirstOrDefault(d => d.ArrearsRows.Any(r => r.Id == arrearsId));
        if (holderOf is null)
        {
          // an opening due paid before the first demand
          var row = await context.ArrearsRows.FirstAsync(r => r.Id == arrearsId, cancellationToken);
          row.Unsettle(a.Amount);
          continue;
        }
        var billed = holderOf.ArrearsRows.First(r => r.Id == arrearsId);
        if (holderOf.Status == DemandStatus.Carried)
          BillOnHolder(holderOf, billed.HeadId, billed.FiscalYear, a.Amount);
        else
        {
          billed.Unsettle(a.Amount);
          holderOf.ReleasePaid(a.Amount, today);
        }
        continue;
      }
      var demand = demands.FirstOrDefault(d => (a.DemandLineId is { } l && d.Lines.Any(x => x.Id == l)) || (a.PenaltyId is { } p && d.Penalties.Any(x => x.Id == p)))
        ?? throw new DomainException("A demand the payment settled was not found.");
      if (demand.Status == DemandStatus.Carried)
        BillOnHolder(demand, a.HeadId, demand.FiscalYear, a.Amount);
      else
        demand.ReleasePaid(a.Amount, today);
    }
    payment.Reverse(reason, actor, now);
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

  private readonly Dictionary<string, int> _endst = new();

  /// The next endorsement number of a circle and year (TAX/C-I/2026-27/0042). The series is read once per request and
  /// counted on from there, so issuing a thousand notices reads it once; the unique index stops two requests taking the same.
  public async Task<string> EndstNoAsync(string circleCode, int fiscalYear, CancellationToken cancellationToken)
  {
    var prefix = $"TAX/{circleCode}/{FiscalYear.Label(fiscalYear)}/";
    if (!_endst.TryGetValue(prefix, out var last))
    {
      var used = await context.Notices.Where(n => n.EndstNo != null && n.EndstNo.StartsWith(prefix)).Select(n => n.EndstNo!).ToListAsync(cancellationToken);
      used.AddRange(context.Notices.Local.Where(n => n.EndstNo != null && n.EndstNo.StartsWith(prefix)).Select(n => n.EndstNo!));
      last = used.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
    }
    _endst[prefix] = last + 1;
    return Numbers.EndstNo(circleCode, fiscalYear, last + 1);
  }

  public async Task<string> NoticeNoAsync(DateOnly issueDate, CancellationToken cancellationToken) =>
      Numbers.NoticeNo(issueDate.Year, await numbers.NextAsync(NumberSequence.NoticeRef, cancellationToken));

  public async Task<string> DemandNoAsync(int fiscalYear, CancellationToken cancellationToken) =>
      Numbers.DemandNo(fiscalYear, await numbers.NextAsync(NumberSequence.DemandNo, cancellationToken));
}

/// The working-day calendar for notice periods, with the HRM holidays of the years involved - read once per request
/// (a run of a thousand notices asks HRM once a year, not once a notice). When HRM cannot be read (down, or no caller
/// token in the daily job) the calendar skips weekends only and says so (FellBack), so a run never fails on it.
public sealed class CalendarProvider(IHrmDirectory hrm, ILogger<CalendarProvider> logger)
{
  private readonly Dictionary<int, IReadOnlySet<DateOnly>> _years = new();

  /// True once a read of HRM's holidays failed in this request; the calendars handed out since skip weekends only.
  public bool FellBack { get; private set; }

  public async Task<WorkCalendar> CalendarAsync(TaxSettings settings, DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    if (!settings.CountWorkingDays || !hrm.IsConfigured)
      return new WorkCalendar(settings.CountWorkingDays, new HashSet<DateOnly>());
    var holidays = new HashSet<DateOnly>();
    for (var year = from.Year; year <= to.Year; year++)
    {
      if (!_years.TryGetValue(year, out var days))
      {
        if (FellBack)
          days = new HashSet<DateOnly>();
        else
        {
          try
          {
            days = await hrm.HolidaysAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);
          }
          catch (ExternalServiceException ex)
          {
            // notice periods then skip weekends only; the run goes on
            logger.LogWarning("HRM holidays not read ({Message}); notice periods count weekends only.", ex.Message);
            FellBack = true;
            days = new HashSet<DateOnly>();
          }
        }
        _years[year] = days;
      }
      holidays.UnionWith(days);
    }
    return new WorkCalendar(true, holidays);
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

  /// The placeholders of a file's SMS when no demand is involved (a payment on the old register's dues).
  public static Dictionary<string, string> FileValues(TaxFile file, string name) => new()
  {
    ["name"] = name,
    ["code"] = file.FileNo,
    ["fy"] = "",
    ["total"] = "",
    ["arrears"] = "",
    ["balance"] = "",
    ["psid"] = Numbers.FormatPsid(file.Psid),
    ["due"] = ""
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
