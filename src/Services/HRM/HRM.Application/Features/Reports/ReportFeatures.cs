using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// ---- establishment and people ----

public sealed record EstablishmentLine(Guid OrgUnitId, string OrgUnit, Guid DesignationId, string Designation, int Bps, int Posts, int Sanctioned, int Filled, int Vacant, int Frozen);
public sealed record EstablishmentReport(DateOnly AsOf, int Posts, int Sanctioned, int Filled, int Vacant, int Frozen, IReadOnlyList<EstablishmentLine> Lines);

/// Sanctioned strength against the people holding it, by unit, designation and BPS (abolished posts left out; frozen
/// seats are neither filled nor vacant). OrgUnitId takes in its sub-units.
public sealed record GetEstablishmentReportQuery(DateOnly? AsOf, Guid? OrgUnitId) : IQuery<Result<EstablishmentReport>>;

public sealed record CountLine(string Key, int Count);
public sealed record HeadcountReport(
  DateOnly AsOf,
  int Total,
  IReadOnlyList<CountLine> ByOrgUnit,
  IReadOnlyList<CountLine> ByBps,
  IReadOnlyList<CountLine> ByEmploymentType,
  IReadOnlyList<CountLine> ByEmploymentStatus,
  IReadOnlyList<CountLine> ByGender);

/// Employees holding a regular post on the date, counted several ways.
public sealed record GetHeadcountReportQuery(DateOnly? AsOf, Guid? OrgUnitId) : IQuery<Result<HeadcountReport>>;

public sealed record RetirementLine(Guid EmployeeId, string EmployeeNumber, string EmployeeName, DateOnly DateOfBirth, DateOnly RetirementDate, int DaysLeft,
  string? PostCode, string? Designation, int? Bps, string? OrgUnit);
public sealed record RetirementsReport(DateOnly From, DateOnly To, int RetirementAge, IReadOnlyList<RetirementLine> Employees, int WithoutDateOfBirth);

/// Employees in service who reach superannuation within the coming months (already past it included), soonest first.
public sealed record GetRetirementsReportQuery(int WithinMonths, Guid? OrgUnitId) : IQuery<Result<RetirementsReport>>;

// ---- leave ----

public sealed record LeaveBalanceLine(Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid LeaveTypeId, string? LeaveType,
  decimal EntitledDays, decimal AccruedDays, decimal UsedDays, decimal BalanceDays);
public sealed record LeaveBalancesReport(int Year, IReadOnlyList<LeaveBalanceLine> Lines);

public sealed record GetLeaveBalancesReportQuery(int Year, Guid? LeaveTypeId, Guid? OrgUnitId) : IQuery<Result<LeaveBalancesReport>>;

// ---- pay ----

public sealed record PayrollMonthLine(int Month, string Period, int Runs, int Slips, decimal GrossPay, decimal TotalDeductions, decimal NetPayable, decimal IncomeTax, decimal GpFund);
public sealed record ComponentTotal(string ComponentCode, string ComponentName, ComponentType ComponentType, decimal Amount);
public sealed record PayrollSummaryReport(int Year, IReadOnlyList<PayrollMonthLine> Months, IReadOnlyList<ComponentTotal> Components, decimal GrossPay, decimal NetPayable);

/// Posted payroll (finalized and paid runs) of a calendar year, month by month and component by component.
public sealed record GetPayrollSummaryReportQuery(int Year) : IQuery<Result<PayrollSummaryReport>>;

public sealed record LoanLine(Guid LoanId, string EmployeeNumber, string EmployeeName, string? LoanType, decimal PrincipalAmount, decimal TotalRepayable,
  decimal Recovered, decimal RemainingBalance, decimal Overdue, DateOnly? NextDueDate, decimal MonthlyInstallment);
public sealed record LoansReport(int Loans, decimal RemainingBalance, decimal Overdue, IReadOnlyList<LoanLine> Lines);

/// Active loans and advances with what is left and what is overdue.
public sealed record GetLoansReportQuery(Guid? LoanTypeId) : IQuery<Result<LoansReport>>;

public sealed record TaxLine(Guid EmployeeId, string EmployeeNumber, string EmployeeName, decimal TaxableIncomeYtd, decimal TaxWithheldYtd, decimal Exemptions);
public sealed record TaxReport(Guid TaxYearId, string YearLabel, decimal TaxableIncome, decimal TaxWithheld, IReadOnlyList<TaxLine> Lines);

/// Taxable pay and tax withheld so far in a tax year (the one in force today by default), per employee.
public sealed record GetTaxReportQuery(Guid? TaxYearId) : IQuery<Result<TaxReport>>;

public sealed record GpFundLine(Guid AccountId, string EmployeeNumber, string EmployeeName, string? AccountNumber, decimal MonthlySubscription,
  decimal TotalSubscribed, decimal TotalInterest, decimal Balance, RecordStatus Status);
public sealed record GpFundReport(int Accounts, decimal Balance, IReadOnlyList<GpFundLine> Lines);

public sealed record GetGpFundReportQuery(bool IncludeClosed) : IQuery<Result<GpFundReport>>;

public class GetRetirementsReportQueryValidator : AbstractValidator<GetRetirementsReportQuery>
{
  public GetRetirementsReportQueryValidator() => RuleFor(x => x.WithinMonths).InclusiveBetween(1, 120);
}

public class ReportHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock, IOptions<HrmOptions> options) :
  IQueryHandler<GetEstablishmentReportQuery, Result<EstablishmentReport>>,
  IQueryHandler<GetHeadcountReportQuery, Result<HeadcountReport>>,
  IQueryHandler<GetRetirementsReportQuery, Result<RetirementsReport>>,
  IQueryHandler<GetLeaveBalancesReportQuery, Result<LeaveBalancesReport>>,
  IQueryHandler<GetPayrollSummaryReportQuery, Result<PayrollSummaryReport>>,
  IQueryHandler<GetLoansReportQuery, Result<LoansReport>>,
  IQueryHandler<GetTaxReportQuery, Result<TaxReport>>,
  IQueryHandler<GetGpFundReportQuery, Result<GpFundReport>>
{
  // ---- establishment and people ----

  public async Task<Result<EstablishmentReport>> Handle(GetEstablishmentReportQuery query, CancellationToken cancellationToken)
  {
    var date = query.AsOf ?? clock.Today;
    var versions = context.PostVersions.AsNoTracking()
      .Where(v => v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date) && v.LifecycleStatus != PostLifecycle.Abolished);
    if (query.OrgUnitId is { } unit)
    {
      var units = (await lookup.SubtreeAsync(OrganizationUnitId.Of(unit), date, cancellationToken)).ToList();
      versions = versions.Where(v => units.Contains(v.OrgUnitId));
    }

    var posts = await versions.ToListAsync(cancellationToken);
    var postIds = posts.Select(p => p.PostId).ToList();
    var filled = (await context.PositionAssignments.AsNoTracking()
        .Where(a => postIds.Contains(a.PostId) && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
          && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
        .Select(a => a.PostId).ToListAsync(cancellationToken))
      .GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());

    var designations = await lookup.DesignationsAsync(posts.Select(p => (DesignationId?)p.DesignationId), cancellationToken);
    var grades = await lookup.GradesAsync(cancellationToken);
    var unitNames = await lookup.UnitNamesAsync(posts.Select(p => (OrganizationUnitId?)p.OrgUnitId), date, cancellationToken);

    var lines = posts
      .GroupBy(p => (p.OrgUnitId, p.DesignationId, p.GradeId))
      .Select(g =>
      {
        var sanctioned = g.Sum(p => p.SanctionedCount);
        var holders = g.Sum(p => filled.GetValueOrDefault(p.PostId));
        var frozen = g.Where(p => p.LifecycleStatus == PostLifecycle.Frozen).Sum(p => Math.Max(0, p.SanctionedCount - filled.GetValueOrDefault(p.PostId)));
        return new EstablishmentLine(g.Key.OrgUnitId.Value, unitNames.GetValueOrDefault(g.Key.OrgUnitId.Value) ?? "", g.Key.DesignationId.Value,
          designations.GetValueOrDefault(g.Key.DesignationId.Value) ?? "", grades.TryGetValue(g.Key.GradeId.Value, out var grade) ? grade.BpsNumber : 0,
          g.Count(), sanctioned, holders, Math.Max(0, sanctioned - holders - frozen), frozen);
      })
      .OrderBy(l => l.OrgUnit).ThenByDescending(l => l.Bps).ThenBy(l => l.Designation)
      .ToList();

    return Result<EstablishmentReport>.Success(new(date, lines.Sum(l => l.Posts), lines.Sum(l => l.Sanctioned), lines.Sum(l => l.Filled),
      lines.Sum(l => l.Vacant), lines.Sum(l => l.Frozen), lines));
  }

  public async Task<Result<HeadcountReport>> Handle(GetHeadcountReportQuery query, CancellationToken cancellationToken)
  {
    var date = query.AsOf ?? clock.Today;
    var ids = await context.PositionAssignments.AsNoTracking()
      .Where(a => a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active && a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
      .Select(a => a.EmployeeId).Distinct().ToListAsync(cancellationToken);

    var placements = await lookup.PlacementsAsync(ids, date, cancellationToken);
    if (query.OrgUnitId is { } unit)
    {
      var units = (await lookup.SubtreeAsync(OrganizationUnitId.Of(unit), date, cancellationToken)).Select(u => u.Value).ToHashSet();
      placements = placements.Where(p => units.Contains(p.Value.OrgUnitId)).ToDictionary(p => p.Key, p => p.Value);
    }

    var employeeIds = placements.Keys.Select(EmployeeId.Of).ToList();
    var people = await context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id))
      .Select(e => new { e.Id, e.EmploymentType, e.EmploymentStatus, e.Gender }).ToListAsync(cancellationToken);

    static List<CountLine> Count<T>(IEnumerable<T> keys, Func<T, string> label) =>
      keys.GroupBy(k => label(k)).Select(g => new CountLine(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).ToList();

    return Result<HeadcountReport>.Success(new(date, people.Count,
      Count(placements.Values, p => p.OrgUnit),
      Count(placements.Values, p => $"BPS-{p.Bps}").OrderByDescending(c => int.Parse(c.Key[4..])).ToList(),
      Count(people, p => EnumText.Label(p.EmploymentType)),
      Count(people, p => EnumText.Label(p.EmploymentStatus)),
      Count(people, p => p.Gender is { } gender ? EnumText.Label(gender) : "Not recorded")));
  }

  public async Task<Result<RetirementsReport>> Handle(GetRetirementsReportQuery query, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var until = today.AddMonths(query.WithinMonths);
    var age = options.Value.RetirementAge;
    var latestBirth = until.AddYears(-age);

    var inService = context.Employees.AsNoTracking().Where(e => e.ProfileStatus == RecordStatus.Active
      && (e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.OnLeave
        || e.EmploymentStatus == EmploymentStatus.Suspended || e.EmploymentStatus == EmploymentStatus.DeputedOut));
    if (query.OrgUnitId is { } unit)
    {
      var inUnit = await lookup.EmployeesInUnitAsync(OrganizationUnitId.Of(unit), today, cancellationToken);
      inService = inService.Where(e => inUnit.Contains(e.Id));
    }

    var due = await inService.Where(e => e.DateOfBirth != null && e.DateOfBirth <= latestBirth).ToListAsync(cancellationToken);
    var unknown = await inService.CountAsync(e => e.DateOfBirth == null, cancellationToken);
    var placements = await lookup.PlacementsAsync(due.Select(e => e.Id), today, cancellationToken);

    var lines = due.Select(e =>
    {
      var retires = e.SuperannuationDate(age)!.Value;
      var place = placements.GetValueOrDefault(e.Id.Value);
      return new RetirementLine(e.Id.Value, e.EmployeeNumber, e.DisplayName, e.DateOfBirth!.Value, retires, retires.DayNumber - today.DayNumber,
        place?.PostCode, place?.Designation, place?.Bps, place?.OrgUnit);
    }).OrderBy(l => l.RetirementDate).ToList();

    return Result<RetirementsReport>.Success(new(today, until, age, lines, unknown));
  }

  // ---- leave ----

  public async Task<Result<LeaveBalancesReport>> Handle(GetLeaveBalancesReportQuery query, CancellationToken cancellationToken)
  {
    var rows = context.LeaveBalances.AsNoTracking().Where(b => b.Year == query.Year);
    if (query.LeaveTypeId is { } type)
      rows = rows.Where(b => b.LeaveTypeId == type);

    var list = await rows.ToListAsync(cancellationToken);
    if (query.OrgUnitId is { } unit)
    {
      var inUnit = await lookup.EmployeesInUnitAsync(OrganizationUnitId.Of(unit), clock.Today, cancellationToken);
      var members = (await inUnit.ToListAsync(cancellationToken)).Select(e => e.Value).ToHashSet();
      list = list.Where(b => members.Contains(b.EmployeeId)).ToList();
    }

    var people = await lookup.EmployeesAsync(list.Select(b => (EmployeeId?)EmployeeId.Of(b.EmployeeId)), cancellationToken);
    var types = await context.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, t => t.Name, cancellationToken);

    return Result<LeaveBalancesReport>.Success(new(query.Year, list.Select(b =>
    {
      var person = people.GetValueOrDefault(b.EmployeeId);
      return new LeaveBalanceLine(b.EmployeeId, person?.EmployeeNumber ?? "", person?.FullName ?? "", b.LeaveTypeId, types.GetValueOrDefault(b.LeaveTypeId),
        b.EntitledDays, b.AccruedDays, b.UsedDays, b.BalanceDays);
    }).OrderBy(l => l.EmployeeNumber).ThenBy(l => l.LeaveType).ToList()));
  }

  // ---- pay ----

  public async Task<Result<PayrollSummaryReport>> Handle(GetPayrollSummaryReportQuery query, CancellationToken cancellationToken)
  {
    var slips = await (
        from t in context.PayrollTransactions.AsNoTracking()
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        where PayrollInputBuilder.Posted.Contains(r.Status)
        join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
        where p.Year == query.Year
        select new { t.Id, RunId = r.Id, p.Month, t.GrossPay, t.TotalDeductions, t.NetPayable })
      .ToListAsync(cancellationToken);
    var slipIds = slips.Select(s => s.Id).ToList();

    var lines = await (
        from l in context.PayrollLines.AsNoTracking()
        where slipIds.Contains(l.PayrollTransactionId)
        join c in context.SalaryComponents on l.SalaryComponentId equals c.Id
        select new { l.PayrollTransactionId, c.ComponentCode, c.ComponentName, l.ComponentType, l.Source, l.CalculatedAmount })
      .ToListAsync(cancellationToken);
    var monthOf = slips.ToDictionary(s => s.Id, s => s.Month);

    var months = slips.GroupBy(s => s.Month).OrderBy(g => g.Key).Select(g => new PayrollMonthLine(g.Key, $"{new DateOnly(query.Year, g.Key, 1):MMMM yyyy}",
        g.Select(s => s.RunId).Distinct().Count(), g.Count(), g.Sum(s => s.GrossPay), g.Sum(s => s.TotalDeductions), g.Sum(s => s.NetPayable),
        lines.Where(l => l.Source == ComponentSource.Tax && monthOf[l.PayrollTransactionId] == g.Key).Sum(l => l.CalculatedAmount),
        lines.Where(l => l.Source == ComponentSource.Gpf && monthOf[l.PayrollTransactionId] == g.Key).Sum(l => l.CalculatedAmount)))
      .ToList();
    var components = lines.GroupBy(l => (l.ComponentCode, l.ComponentName, l.ComponentType))
      .Select(g => new ComponentTotal(g.Key.ComponentCode, g.Key.ComponentName, g.Key.ComponentType, g.Sum(l => l.CalculatedAmount)))
      .OrderBy(c => c.ComponentType).ThenByDescending(c => c.Amount).ToList();

    return Result<PayrollSummaryReport>.Success(new(query.Year, months, components, slips.Sum(s => s.GrossPay), slips.Sum(s => s.NetPayable)));
  }

  public async Task<Result<LoansReport>> Handle(GetLoansReportQuery query, CancellationToken cancellationToken)
  {
    var rows = context.Loans.AsNoTracking().Include(l => l.Installments).Where(l => l.Status == LoanStatus.Active);
    if (query.LoanTypeId is { } type)
    {
      var typeId = LoanTypeId.Of(type);
      rows = rows.Where(l => l.LoanTypeId == typeId);
    }

    var loans = await rows.ToListAsync(cancellationToken);
    var people = await lookup.EmployeesAsync(loans.Select(l => (EmployeeId?)l.EmployeeId), cancellationToken);
    var types = await context.LoanTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    var today = clock.Today;

    var lines = loans.Select(l =>
    {
      var person = people.GetValueOrDefault(l.EmployeeId.Value);
      var open = l.Installments.Where(i => i.IsOpen).ToList();
      return new LoanLine(l.Id.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", types.GetValueOrDefault(l.LoanTypeId), l.PrincipalAmount, l.TotalRepayable,
        l.Installments.Sum(i => i.PaidAmount), l.RemainingBalance, open.Where(i => i.DueDate <= today).Sum(i => i.Outstanding),
        open.Where(i => i.DueDate > today).Select(i => (DateOnly?)i.DueDate).Min(), l.MonthlyInstallment);
    }).OrderBy(l => l.EmployeeNumber).ThenBy(l => l.LoanType).ToList();

    return Result<LoansReport>.Success(new(lines.Count, lines.Sum(l => l.RemainingBalance), lines.Sum(l => l.Overdue), lines));
  }

  public async Task<Result<TaxReport>> Handle(GetTaxReportQuery query, CancellationToken cancellationToken)
  {
    TaxYear year;
    if (query.TaxYearId is { } given)
      year = await context.LoadTaxYearAsync(given, cancellationToken);
    else
    {
      var today = clock.Today;
      year = await context.TaxYears.AsNoTracking().FirstOrDefaultAsync(t => t.StartDate <= today && t.EndDate >= today, cancellationToken)
        ?? throw new TaxYearNotFoundException($"No tax year covers {today:yyyy-MM-dd}.");
    }

    var ytd = await context.EmployeeTaxYtd.AsNoTracking().Where(t => t.TaxYearId == year.Id.Value).ToListAsync(cancellationToken);
    var exemptions = (await context.TaxExemptions.AsNoTracking().Where(e => e.TaxYearId == year.Id).Select(e => new { e.EmployeeId, e.Amount }).ToListAsync(cancellationToken))
      .GroupBy(e => e.EmployeeId.Value).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
    var people = await lookup.EmployeesAsync(ytd.Select(t => (EmployeeId?)EmployeeId.Of(t.EmployeeId)), cancellationToken);

    var lines = ytd.Select(t =>
    {
      var person = people.GetValueOrDefault(t.EmployeeId);
      return new TaxLine(t.EmployeeId, person?.EmployeeNumber ?? "", person?.FullName ?? "", t.TaxableIncomeYtd, t.TaxWithheldYtd, exemptions.GetValueOrDefault(t.EmployeeId));
    }).OrderBy(l => l.EmployeeNumber).ToList();

    return Result<TaxReport>.Success(new(year.Id.Value, year.YearLabel, lines.Sum(l => l.TaxableIncomeYtd), lines.Sum(l => l.TaxWithheldYtd), lines));
  }

  public async Task<Result<GpFundReport>> Handle(GetGpFundReportQuery query, CancellationToken cancellationToken)
  {
    var accounts = await context.GpFundAccounts.AsNoTracking().Where(a => query.IncludeClosed || a.Status == RecordStatus.Active).ToListAsync(cancellationToken);
    var balances = await context.GpFundBalances.AsNoTracking().ToDictionaryAsync(b => b.GpFundAccountId, cancellationToken);
    var people = await lookup.EmployeesAsync(accounts.Select(a => (EmployeeId?)a.EmployeeId), cancellationToken);

    var lines = accounts.Select(a =>
    {
      var person = people.GetValueOrDefault(a.EmployeeId.Value);
      var balance = balances.GetValueOrDefault(a.Id.Value);
      return new GpFundLine(a.Id.Value, person?.EmployeeNumber ?? "", person?.FullName ?? "", a.AccountNumber, a.MonthlySubscription,
        balance?.TotalSubscribed ?? 0, balance?.TotalInterest ?? 0, balance?.Balance ?? 0, a.Status);
    }).OrderBy(l => l.EmployeeNumber).ToList();

    return Result<GpFundReport>.Success(new(lines.Count, lines.Sum(l => l.Balance), lines));
  }
}
