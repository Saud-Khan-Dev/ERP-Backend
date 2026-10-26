using Microsoft.EntityFrameworkCore;

// ---- reports ----

public sealed record GetCollectionSummaryQuery(int FiscalYear) : IQuery<Result<CollectionSummaryDto>>;

/// The year by head: what was demanded (this year's charges, of which the annual increase and the minimum top-ups), the
/// earlier years' dues billed under the head, the surcharges, and what was recovered of each.
public sealed record GetHeadReportQuery(int FiscalYear, Guid? CircleId) : IQuery<Result<HeadReportDto>>;

/// The year by circle and town (the town's circle on 1 July of the year).
public sealed record GetCircleTownReportQuery(int FiscalYear) : IQuery<Result<CircleTownReportDto>>;

/// Every file that owes something, over all years: the years involved, the last payment, the stage reached.
public sealed record GetArrearsReportQuery(PaginationRequest Pagination, Guid? CircleId, decimal? MinAmount, string? Search) : IQuery<Result<ArrearsReportDto>>;

/// Money received in a period, day by day or month by month (Pakistan time): the dues behind references and fee receipts.
public sealed record GetCollectionByPeriodQuery(DateOnly From, DateOnly To, string GroupBy, Guid? CircleId) : IQuery<Result<CollectionByPeriodDto>>;

/// The Taxation overview: the year at a glance, worked out on the server.
public sealed record GetTaxOverviewQuery(int? FiscalYear) : IQuery<Result<TaxOverviewDto>>;

public class ReportHandlers(IApplicationDbContext context, TaxSetup setup, IClock clock) :
  IQueryHandler<GetCollectionSummaryQuery, Result<CollectionSummaryDto>>,
  IQueryHandler<GetHeadReportQuery, Result<HeadReportDto>>,
  IQueryHandler<GetCircleTownReportQuery, Result<CircleTownReportDto>>,
  IQueryHandler<GetArrearsReportQuery, Result<ArrearsReportDto>>,
  IQueryHandler<GetCollectionByPeriodQuery, Result<CollectionByPeriodDto>>,
  IQueryHandler<GetTaxOverviewQuery, Result<TaxOverviewDto>>
{
  public async Task<Result<CollectionSummaryDto>> Handle(GetCollectionSummaryQuery query, CancellationToken cancellationToken)
  {
    var fy = FiscalYear.Valid(query.FiscalYear);
    var year = await context.Demands.AsNoTracking().InScope(context)
      .Where(d => d.FiscalYear == fy && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled)
      .Select(d => new { d.Id, d.TaxFileId, d.Status, d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount, d.PaidAmount, d.Balance })
      .ToListAsync(cancellationToken);
    var circleOf = await context.ScopedFiles().AsNoTracking().Select(f => new { f.Id, f.CircleId }).ToDictionaryAsync(f => f.Id, f => f.CircleId, cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);

    var outstanding = await context.Demands.AsNoTracking().InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted)).SumAsync(d => (decimal?)d.Balance, cancellationToken) ?? 0m;
    // the surcharges on this year's demands
    var ids = year.Select(d => d.Id).ToList();
    var penalties = await context.Penalties.AsNoTracking().InScope(context).Where(p => ids.Contains(p.DemandId))
      .GroupBy(p => p.Kind).Select(g => new { g.Key, Total = g.Sum(p => p.Amount) }).ToListAsync(cancellationToken);
    var pending = await context.Notices.InScope(context).CountAsync(n => n.Status == NoticeStatus.Pending, cancellationToken);

    decimal Charged(decimal current, decimal arrears, decimal penalty) => current + arrears + penalty;
    var demanded = year.Sum(d => Charged(d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount));
    var recovered = year.Sum(d => d.PaidAmount);
    var byCircle = circles.Select(c =>
    {
      var mine = year.Where(d => circleOf.GetValueOrDefault(d.TaxFileId) == c.Id).ToList();
      return new CircleCollectionDto(c.Id.Value, c.Code, mine.Count, mine.Count(d => d.Status == DemandStatus.Paid),
        mine.Sum(d => Charged(d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount)), mine.Sum(d => d.PaidAmount), mine.Sum(d => d.Balance));
    }).ToList();

    return Result<CollectionSummaryDto>.Success(new CollectionSummaryDto(fy, FiscalYear.Label(fy), circleOf.Count, year.Count,
      year.Count(d => d.Status == DemandStatus.Paid), year.Count(d => d.Status == DemandStatus.PartPaid),
      year.Count(d => d.Status == DemandStatus.Issued), year.Count(d => d.Status == DemandStatus.Defaulted), demanded, recovered,
      demanded > 0 ? (int)Math.Round(100m * recovered / demanded) : 0, outstanding,
      penalties.FirstOrDefault(p => p.Key == PenaltyKind.IntraYear)?.Total ?? 0, penalties.FirstOrDefault(p => p.Key == PenaltyKind.YearEnd)?.Total ?? 0,
      pending, byCircle));
  }

  public async Task<Result<HeadReportDto>> Handle(GetHeadReportQuery query, CancellationToken cancellationToken)
  {
    var fy = FiscalYear.Valid(query.FiscalYear);
    var demands = context.Demands.AsNoTracking().InScope(context).Where(d => d.FiscalYear == fy && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled);
    if (query.CircleId is { } c)
    {
      var circleId = TaxCircleId.Of(c);
      var inCircle = context.TaxFiles.Where(f => f.CircleId == circleId).Select(f => f.Id);
      demands = demands.Where(d => inCircle.Contains(d.TaxFileId));
    }
    var demandIds = demands.Select(d => d.Id);
    var posted = context.Payments.Where(p => p.Status == PaymentStatus.Posted).Select(p => p.Id);

    var lines = await context.DemandLines.AsNoTracking().Where(l => demandIds.Contains(l.DemandId))
      .GroupBy(l => l.HeadId).Select(g => new { g.Key, Amount = g.Sum(l => l.Amount), Increase = g.Sum(l => l.IncreaseAmount), TopUp = g.Sum(l => l.OverrideAmount),
        Lines = g.Count() }).ToListAsync(cancellationToken);
    var linePaid = await context.PaymentAllocations.AsNoTracking()
      .Where(a => posted.Contains(a.PaymentId) && a.DemandLineId != null && context.DemandLines.Any(l => l.Id == a.DemandLineId && demandIds.Contains(l.DemandId)))
      .GroupBy(a => a.HeadId).Select(g => new { g.Key, Paid = g.Sum(a => a.Amount) }).ToListAsync(cancellationToken);
    var arrears = await context.ArrearsRows.AsNoTracking().InScope(context).Where(a => a.AddedToDemandId != null && demandIds.Contains(a.AddedToDemandId))
      .GroupBy(a => a.HeadId).Select(g => new { g.Key, Amount = g.Sum(a => a.OriginalAmount + a.PenaltyAmount), Outstanding = g.Sum(a => a.Outstanding) })
      .ToListAsync(cancellationToken);
    var penalties = await context.Penalties.AsNoTracking().InScope(context).Where(p => demandIds.Contains(p.DemandId))
      .GroupBy(p => p.Kind).Select(g => new { g.Key, Amount = g.Sum(p => p.Amount) }).ToListAsync(cancellationToken);
    var penaltyPaid = await context.PaymentAllocations.AsNoTracking()
      .Where(a => posted.Contains(a.PaymentId) && a.PenaltyId != null && context.Penalties.Any(p => p.Id == a.PenaltyId && demandIds.Contains(p.DemandId)))
      .SumAsync(a => (decimal?)a.Amount, cancellationToken) ?? 0m;

    var heads = await setup.HeadsAsync(cancellationToken);
    var surcharge = heads.First(h => h.Code == HeadCodes.Surcharge);
    var rows = heads.Select(h =>
    {
      var l = lines.FirstOrDefault(x => x.Key == h.Id);
      var a = arrears.FirstOrDefault(x => x.Key == h.Id);
      var surcharges = h.Id == surcharge.Id ? penalties.Sum(p => p.Amount) : 0m;
      var demanded = (l?.Amount ?? 0m) + (a?.Amount ?? 0m) + surcharges;
      var recovered = (linePaid.FirstOrDefault(x => x.Key == h.Id)?.Paid ?? 0m) + ((a?.Amount ?? 0m) - (a?.Outstanding ?? 0m)) + (h.Id == surcharge.Id ? penaltyPaid : 0m);
      return new HeadReportRowDto(h.Id.Value, h.Code, h.Name, h.HeadKind, l?.Lines ?? 0, l?.Amount ?? 0m, l?.Increase ?? 0m, Math.Max(0, l?.TopUp ?? 0m), a?.Amount ?? 0m,
        surcharges, demanded, recovered, demanded - recovered);
    }).Where(r => r.Demanded > 0 || r.Recovered > 0).ToList();
    return Result<HeadReportDto>.Success(new HeadReportDto(fy, FiscalYear.Label(fy), rows,
      penalties.FirstOrDefault(p => p.Key == PenaltyKind.IntraYear)?.Amount ?? 0m, penalties.FirstOrDefault(p => p.Key == PenaltyKind.YearEnd)?.Amount ?? 0m));
  }

  public async Task<Result<CircleTownReportDto>> Handle(GetCircleTownReportQuery query, CancellationToken cancellationToken)
  {
    var fy = FiscalYear.Valid(query.FiscalYear);
    var start = FiscalYear.Start(fy);
    var year = await context.Demands.AsNoTracking().InScope(context)
      .Where(d => d.FiscalYear == fy && d.Status != DemandStatus.Draft && d.Status != DemandStatus.Cancelled)
      .Join(context.TaxFiles, d => d.TaxFileId, f => f.Id, (d, f) => new { f.TownId, d.Status, d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount, d.PaidAmount, d.Balance })
      .ToListAsync(cancellationToken);
    var files = await context.ScopedFiles().AsNoTracking().Where(f => f.TaxStatus != TaxStatus.Closed).GroupBy(f => f.TownId)
      .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
    var towns = await context.CircleTowns.AsNoTracking().ToListAsync(cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

    // the town's circle on 1 July of the year (or its latest one)
    var townIds = year.Select(y => y.TownId).Concat(files.Select(f => f.Key)).Distinct().ToList();
    var rows = townIds.Select(t =>
    {
      var history = towns.Where(x => x.TownId == t).ToList();
      var at = history.FirstOrDefault(x => x.Covers(start)) ?? history.OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
      var mine = year.Where(y => y.TownId == t).ToList();
      var demanded = mine.Sum(y => y.CurrentAmount + y.ArrearsBf + y.PenaltyAmount);
      var recovered = mine.Sum(y => y.PaidAmount);
      return new CircleTownRowDto(at?.CircleId.Value, at is null ? null : circles.GetValueOrDefault(at.CircleId), t, at?.TownCode ?? "", at?.ShortCode,
        files.FirstOrDefault(f => f.Key == t)?.Count ?? 0, mine.Count, mine.Count(y => y.Status == DemandStatus.Paid), demanded, recovered, mine.Sum(y => y.Balance),
        demanded > 0 ? (int)Math.Round(100m * recovered / demanded) : 0);
    }).OrderBy(r => r.CircleCode).ThenBy(r => r.TownCode).ToList();
    return Result<CircleTownReportDto>.Success(new CircleTownReportDto(fy, FiscalYear.Label(fy), rows));
  }

  public async Task<Result<ArrearsReportDto>> Handle(GetArrearsReportQuery query, CancellationToken cancellationToken)
  {
    var owing = context.Demands.AsNoTracking().InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted) && d.Balance > 0);
    if (query.CircleId is { } c)
    {
      var circleId = TaxCircleId.Of(c);
      var inCircle = context.TaxFiles.Where(f => f.CircleId == circleId).Select(f => f.Id);
      owing = owing.Where(d => inCircle.Contains(d.TaxFileId));
    }
    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var s = query.Search.Trim().ToUpperInvariant();
      var files = context.TaxFiles.Where(f => f.FileNo.ToUpper().Contains(s) || f.PropertyCode.ToUpper().Contains(s)).Select(f => f.Id);
      owing = owing.Where(d => files.Contains(d.TaxFileId) || d.PayerName.ToUpper().Contains(s));
    }
    var byFile = owing.GroupBy(d => d.TaxFileId).Select(g => new { FileId = g.Key, Outstanding = g.Sum(d => d.Balance) });
    if (query.MinAmount is { } min)
      byFile = byFile.Where(x => x.Outstanding >= min);
    var total = await byFile.LongCountAsync(cancellationToken);
    var sum = await byFile.SumAsync(x => (decimal?)x.Outstanding, cancellationToken) ?? 0m;
    var page = await byFile.OrderByDescending(x => x.Outstanding).Skip(query.Pagination.Pageindex * query.Pagination.PageSize).Take(query.Pagination.PageSize)
      .ToListAsync(cancellationToken);

    var ids = page.Select(p => p.FileId).ToList();
    var files2 = await context.ScopedFiles().AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new { f.Id, f.FileNo, f.CircleId, f.Psid }).ToDictionaryAsync(f => f.Id, cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
    var open = await context.Demands.AsNoTracking().InScope(context).Where(d => ids.Contains(d.TaxFileId) && (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted) && d.Balance > 0)
      .Select(d => new { d.Id, d.TaxFileId, d.FiscalYear, d.PayerName, d.Stage, d.Balance }).ToListAsync(cancellationToken);
    var openIds = open.Select(d => d.Id).ToList();
    var years = await context.ArrearsRows.AsNoTracking().InScope(context).Where(a => a.AddedToDemandId != null && openIds.Contains(a.AddedToDemandId) && a.Outstanding > 0)
      .Select(a => new { a.AddedToDemandId, a.FiscalYear }).ToListAsync(cancellationToken);
    var lastPaid = await context.Payments.AsNoTracking().InScope(context).Where(p => p.TaxFileId != null && ids.Contains(p.TaxFileId) && p.Status == PaymentStatus.Posted && p.FeeId == null)
      .GroupBy(p => p.TaxFileId).Select(g => new { g.Key, At = g.Max(p => p.PaidAt) }).ToListAsync(cancellationToken);

    var rows = page.Select(p =>
    {
      var f = files2[p.FileId];
      var mine = open.Where(d => d.TaxFileId == p.FileId).OrderByDescending(d => d.FiscalYear).ToList();
      var involved = mine.Select(d => d.FiscalYear)
        .Concat(years.Where(y => mine.Any(d => d.Id == y.AddedToDemandId) && y.FiscalYear is not null).Select(y => y.FiscalYear!.Value))
        .Distinct().OrderBy(y => y).ToList();
      var latest = mine.FirstOrDefault();
      return new ArrearsReportRowDto(p.FileId.Value, f.FileNo, f.Psid, f.CircleId.Value, circles.GetValueOrDefault(f.CircleId), latest?.PayerName ?? "",
        involved.Select(FiscalYear.Label).ToList(), involved.Count, lastPaid.FirstOrDefault(x => x.Key == p.FileId)?.At, latest?.Stage, p.Outstanding);
    }).ToList();
    return Result<ArrearsReportDto>.Success(new ArrearsReportDto(new PaginatedResult<ArrearsReportRowDto>(query.Pagination.Pageindex, query.Pagination.PageSize, total, rows), sum));
  }

  public async Task<Result<CollectionByPeriodDto>> Handle(GetCollectionByPeriodQuery query, CancellationToken cancellationToken)
  {
    if (query.To < query.From)
      return Result<CollectionByPeriodDto>.Failure("The period ends before it starts.");
    if (query.To.DayNumber - query.From.DayNumber > 3660)
      return Result<CollectionByPeriodDto>.Failure("Choose a period of at most ten years.");
    var monthly = string.Equals(query.GroupBy, "month", StringComparison.OrdinalIgnoreCase);
    var start = Pkt.StartOf(query.From);
    var end = Pkt.StartOf(query.To.AddDays(1));
    var rows = context.Payments.AsNoTracking().InScope(context).Where(p => p.Status == PaymentStatus.Posted && p.PaidAt >= start && p.PaidAt < end);
    if (query.CircleId is { } c)
    {
      var circleId = TaxCircleId.Of(c);
      var inCircle = context.TaxFiles.Where(f => f.CircleId == circleId).Select(f => f.Id);
      rows = rows.Where(p => p.TaxFileId != null && inCircle.Contains(p.TaxFileId));
    }
    var list = await rows.Select(p => new { p.PaidAt, p.Amount, Fee = p.FeeId != null, p.Channel }).ToListAsync(cancellationToken);

    // the day in Pakistan time each payment fell on
    var periods = list.GroupBy(p =>
      {
        var day = Pkt.DayOf(p.PaidAt);
        return monthly ? new DateOnly(day.Year, day.Month, 1) : day;
      })
      .OrderBy(g => g.Key)
      .Select(g => new CollectionPeriodDto(g.Key, monthly ? g.Key.ToString("MMM yyyy") : g.Key.ToString("dd'/'MM'/'yyyy"), g.Count(),
        g.Where(p => !p.Fee).Sum(p => p.Amount), g.Where(p => p.Fee).Sum(p => p.Amount), g.Sum(p => p.Amount)))
      .ToList();
    var byChannel = list.GroupBy(p => p.Channel).Select(g => new CollectionChannelDto(g.Key, g.Count(), g.Sum(p => p.Amount))).OrderByDescending(x => x.Amount).ToList();
    return Result<CollectionByPeriodDto>.Success(new CollectionByPeriodDto(query.From, query.To, monthly ? "month" : "day", periods, byChannel,
      list.Where(p => !p.Fee).Sum(p => p.Amount), list.Where(p => p.Fee).Sum(p => p.Amount)));
  }

  public async Task<Result<TaxOverviewDto>> Handle(GetTaxOverviewQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var fy = FiscalYear.Valid(query.FiscalYear ?? FiscalYear.Of(today));
    var label = FiscalYear.Label(fy);

    var files = await context.ScopedFiles().AsNoTracking().GroupBy(f => f.TaxStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
    var onHold = await context.Holds.AsNoTracking().InScope(context).Where(h => h.SuspendsDemand && h.StartedOn <= today && (h.EndedOn == null || h.EndedOn >= today))
      .Select(h => h.TaxFileId).Distinct().CountAsync(cancellationToken);
    var byCategory = await context.ScopedFiles().AsNoTracking().Where(f => f.TaxStatus != TaxStatus.Closed).GroupBy(f => f.TaxCategory)
      .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);

    var year = await context.Demands.AsNoTracking().InScope(context).Where(d => d.FiscalYear == fy && d.Status != DemandStatus.Cancelled)
      .Select(d => new { d.Id, d.TaxFileId, d.Status, d.Stage, d.CurrentAmount, d.ArrearsBf, d.PenaltyAmount, d.PaidAmount, d.Balance })
      .ToListAsync(cancellationToken);
    var issued = year.Where(d => d.Status != DemandStatus.Draft).ToList();
    var demanded = issued.Sum(d => d.CurrentAmount + d.ArrearsBf + d.PenaltyAmount);
    var recovered = issued.Sum(d => d.PaidAmount);
    var outstandingAll = await context.Demands.AsNoTracking().InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted)).SumAsync(d => (decimal?)d.Balance, cancellationToken) ?? 0m;

    var circleOf = await context.ScopedFiles().AsNoTracking().Select(f => new { f.Id, f.CircleId }).ToDictionaryAsync(f => f.Id, f => f.CircleId, cancellationToken);
    var circles = await context.TaxCircles.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
    var byCircle = circles.Select(c =>
    {
      var mine = issued.Where(d => circleOf.GetValueOrDefault(d.TaxFileId) == c.Id).ToList();
      return new CircleCollectionDto(c.Id.Value, c.Code, mine.Count, mine.Count(d => d.Status == DemandStatus.Paid),
        mine.Sum(d => d.CurrentAmount + d.ArrearsBf + d.PenaltyAmount), mine.Sum(d => d.PaidAmount), mine.Sum(d => d.Balance));
    }).ToList();

    // money received in the year, month by month (Pakistan time), taxes and fees
    var start = Pkt.StartOf(FiscalYear.Start(fy));
    var end = Pkt.StartOf(FiscalYear.End(fy).AddDays(1));
    var paid = await context.Payments.AsNoTracking().InScope(context).Where(p => p.Status == PaymentStatus.Posted && p.PaidAt >= start && p.PaidAt < end)
      .Select(p => new { p.PaidAt, p.Amount, Fee = p.FeeId != null }).ToListAsync(cancellationToken);
    var months = Enumerable.Range(0, 12).Select(i => FiscalYear.Start(fy).AddMonths(i)).Select(m =>
    {
      var mine = paid.Where(p => { var d = Pkt.DayOf(p.PaidAt); return d.Year == m.Year && d.Month == m.Month; }).ToList();
      return new CollectionPeriodDto(m, m.ToString("MMM yyyy"), mine.Count, mine.Where(p => !p.Fee).Sum(p => p.Amount), mine.Where(p => p.Fee).Sum(p => p.Amount),
        mine.Sum(p => p.Amount));
    }).ToList();

    // the year's demand by head
    var yearIds = issued.Select(d => d.Id).ToList();
    var heads = (await setup.HeadsAsync(cancellationToken)).ToDictionary(h => h.Id);
    var byHead = (await context.DemandLines.AsNoTracking().Where(l => yearIds.Contains(l.DemandId)).GroupBy(l => l.HeadId)
        .Select(g => new { g.Key, Amount = g.Sum(l => l.Amount) }).ToListAsync(cancellationToken))
      .Select(x => new HeadAmountDto(x.Key.Value, heads.GetValueOrDefault(x.Key)?.Code ?? "", heads.GetValueOrDefault(x.Key)?.Name ?? "", x.Amount))
      .OrderByDescending(x => x.Amount).ToList();

    var notices = await context.Notices.AsNoTracking().InScope(context).GroupBy(n => new { n.Status, n.Stage }).Select(g => new { g.Key.Status, g.Key.Stage, Count = g.Count() })
      .ToListAsync(cancellationToken);
    var unserved = await context.Notices.InScope(context).CountAsync(n => n.Status == NoticeStatus.Issued && n.ServedOn == null, cancellationToken);

    var feesDue = await context.Fees.AsNoTracking().InScope(context).Where(f => f.Status == FeeStatus.Due).GroupBy(f => 1)
      .Select(g => new { Count = g.Count(), Amount = g.Sum(f => f.Amount) }).FirstOrDefaultAsync(cancellationToken);
    var validClearances = await context.Clearances.InScope(context).CountAsync(c => c.Status == ClearanceStatus.Issued && c.ValidUntil >= today, cancellationToken);
    var syncsWaiting = await context.PsidSyncs.InScope(context).CountAsync(s => s.Status == PsidSyncStatus.Pending, cancellationToken);
    var smsQueued = await context.SmsMessages.InScope(context).CountAsync(m => m.DeliveryStatus == SmsStatus.Queued, cancellationToken);
    var penalties = await context.Penalties.AsNoTracking().InScope(context).Where(p => yearIds.Contains(p.DemandId)).GroupBy(p => p.Kind)
      .Select(g => new { g.Key, Amount = g.Sum(p => p.Amount) }).ToListAsync(cancellationToken);

    // the largest balances behind references now
    var largest = await context.Demands.AsNoTracking().InScope(context).Where(d => (d.Status == DemandStatus.Issued || d.Status == DemandStatus.PartPaid || d.Status == DemandStatus.Defaulted) && d.Balance > 0).GroupBy(d => d.TaxFileId)
      .Select(g => new { FileId = g.Key, Outstanding = g.Sum(d => d.Balance) }).OrderByDescending(x => x.Outstanding).Take(10).ToListAsync(cancellationToken);
    var largestIds = largest.Select(l => l.FileId).ToList();
    var largestFiles = await context.ScopedFiles().AsNoTracking().Where(f => largestIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileNo, cancellationToken);
    var largestPayers = await context.TaxFilePayers.AsNoTracking().Where(p => largestIds.Contains(p.TaxFileId) && p.EffectiveTo == null && p.IsAddressee)
      .Select(p => new { p.TaxFileId, p.Name }).ToListAsync(cancellationToken);

    // the in-year surcharge: its period over for the year and no run of the year yet
    var rule = await setup.RuleAsync(PenaltyKind.IntraYear, today, cancellationToken);
    var settings = await setup.SettingsAsync(today, cancellationToken);
    DateOnly? surchargeFrom = rule is null ? null : rule.EligibleFrom(fy, null, settings.DueDate(fy));
    var surchargeRun = await context.PenaltyRuns.AnyAsync(r => r.FiscalYear == fy, cancellationToken);

    var lastJob = await context.JobRuns.AsNoTracking().Where(j => j.Job == JobRun.Daily).OrderByDescending(j => j.StartedAt).FirstOrDefaultAsync(cancellationToken);

    return Result<TaxOverviewDto>.Success(new TaxOverviewDto(fy, label,
      new FileCountsDto(files.Sum(f => f.Count), files.FirstOrDefault(f => f.Key == TaxStatus.Active)?.Count ?? 0, files.FirstOrDefault(f => f.Key == TaxStatus.Exempt)?.Count ?? 0,
        files.FirstOrDefault(f => f.Key == TaxStatus.Closed)?.Count ?? 0, onHold, byCategory.Select(c => new CategoryCountDto(c.Key, c.Count)).ToList()),
      new DemandCountsDto(year.Count(d => d.Status == DemandStatus.Draft), issued.Count, issued.Count(d => d.Status == DemandStatus.Issued),
        issued.Count(d => d.Status == DemandStatus.PartPaid), issued.Count(d => d.Status == DemandStatus.Paid), issued.Count(d => d.Status == DemandStatus.Defaulted),
        issued.Count(d => d.Status == DemandStatus.Carried)),
      demanded, recovered, demanded > 0 ? (int)Math.Round(100m * recovered / demanded) : 0, issued.Sum(d => d.Balance), outstandingAll,
      byCircle, months, byHead,
      Enum.GetValues<NoticeStage>().Select(st => new StageCountDto(st, notices.Where(n => n.Stage == st && n.Status == NoticeStatus.Issued).Sum(n => n.Count),
        notices.Where(n => n.Stage == st && n.Status == NoticeStatus.Pending).Sum(n => n.Count))).ToList(),
      issued.Where(d => d.Stage is not null).GroupBy(d => d.Stage!.Value).Select(g => new DemandStageCountDto(g.Key, g.Count())).ToList(),
      notices.Where(n => n.Status == NoticeStatus.Pending).Sum(n => n.Count), unserved,
      feesDue?.Count ?? 0, feesDue?.Amount ?? 0m, paid.Where(p => p.Fee).Sum(p => p.Amount), validClearances, syncsWaiting, smsQueued,
      penalties.FirstOrDefault(p => p.Key == PenaltyKind.IntraYear)?.Amount ?? 0m, penalties.FirstOrDefault(p => p.Key == PenaltyKind.YearEnd)?.Amount ?? 0m,
      largest.Select(l => new LargestBalanceDto(l.FileId.Value, largestFiles.GetValueOrDefault(l.FileId) ?? "",
        largestPayers.FirstOrDefault(p => p.TaxFileId == l.FileId)?.Name, l.Outstanding)).ToList(),
      surchargeFrom, surchargeRun, fy == FiscalYear.Of(today) && surchargeFrom is { } from && from <= today && !surchargeRun,
      lastJob?.ToDto()));
  }
}
