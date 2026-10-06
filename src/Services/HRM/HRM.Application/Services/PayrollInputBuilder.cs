using Microsoft.EntityFrameworkCore;

/// Loads what the payroll engine needs for a run: the salary catalogue and, for many employees at once, their posts,
/// pay, unpaid leave, overrides, GP Fund subscription, loan installments due and tax position. A run of any size costs a
/// fixed number of queries.
public class PayrollInputBuilder(IApplicationDbContext context)
{
  /// Runs whose figures are posted (tax withheld, recoveries made).
  public static readonly PayrollRunStatus[] Posted = [PayrollRunStatus.Finalized, PayrollRunStatus.Paid];

  public async Task<PayrollCatalog> CatalogAsync(PayrollPeriod period, CancellationToken cancellationToken)
  {
    var components = await context.SalaryComponents.AsNoTracking().ToListAsync(cancellationToken);
    var rules = await context.SalaryRules.AsNoTracking()
      .Where(r => r.Status == RecordStatus.Active && r.EffectiveFrom <= period.EndDate && (r.EffectiveTo == null || r.EffectiveTo >= period.StartDate))
      .ToListAsync(cancellationToken);
    var loanComponents = (await context.LoanTypes.AsNoTracking().Where(t => t.SalaryComponentId != null)
      .Select(t => t.SalaryComponentId!).ToListAsync(cancellationToken)).ToHashSet();

    SalaryComponent System(string code) => components.FirstOrDefault(c => c.ComponentCode == code)
      ?? throw new DomainException($"The {code} salary component is missing; restart the HRM service to restore it.");

    return new PayrollCatalog(components, rules, System(SystemComponents.BasicPay), System(SystemComponents.IncomeTax),
      components.FirstOrDefault(c => c.ComponentCode == SystemComponents.GpFund), loanComponents);
  }

  /// Employees holding a regular post at some point of the period.
  public Task<List<EmployeeId>> PostedEmployeesAsync(PayrollPeriod period, CancellationToken cancellationToken) =>
      context.PositionAssignments.AsNoTracking()
        .Where(a => a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
          && a.EffectiveFrom <= period.EndDate && (a.EffectiveTo == null || a.EffectiveTo >= period.StartDate))
        .Select(a => a.EmployeeId).Distinct().ToListAsync(cancellationToken);

  /// The tax year the period's pay is taxed in (the active one covering its last day), with its slabs.
  public Task<TaxYear?> TaxYearAsync(PayrollPeriod period, CancellationToken cancellationToken) =>
      context.TaxYears.AsNoTracking().Include(t => t.Slabs)
        .FirstOrDefaultAsync(t => t.Status == RecordStatus.Active && t.StartDate <= period.EndDate && t.EndDate >= period.EndDate, cancellationToken);

  public async Task<Dictionary<EmployeeId, PayrollEmployeeInput>> InputsAsync(
      PayrollRun run,
      PayrollPeriod period,
      IReadOnlyCollection<Employee> employees,
      IReadOnlyDictionary<EmployeeId, PayrollTransaction> existing,
      TaxYear? taxYear,
      CancellationToken cancellationToken)
  {
    var ids = employees.Select(e => e.Id).ToList();
    var start = period.StartDate;
    var end = period.EndDate;

    // ---- posts held and what they were ----
    var assignments = await context.PositionAssignments.AsNoTracking()
      .Where(a => ids.Contains(a.EmployeeId) && a.AssignmentType == AssignmentType.Regular && a.Status == RecordStatus.Active
        && a.EffectiveFrom <= end && (a.EffectiveTo == null || a.EffectiveTo >= start))
      .ToListAsync(cancellationToken);
    var postIds = assignments.Select(a => a.PostId).Distinct().ToList();
    var versions = await (
        from v in context.PostVersions.AsNoTracking()
        where postIds.Contains(v.PostId) && v.EffectiveFrom <= end && (v.EffectiveTo == null || v.EffectiveTo >= start)
        join g in context.PayScaleGrades on v.GradeId equals g.Id
        select new PostVersionSlice(v.PostId, v.GradeId, g.BpsNumber, v.DesignationId, v.OrgUnitId, v.EffectiveFrom, v.EffectiveTo))
      .ToListAsync(cancellationToken);

    // ---- pay ----
    var pay = await (
        from p in context.PayRecords.AsNoTracking()
        where ids.Contains(p.EmployeeId) && p.EffectiveFrom <= end && (p.EffectiveTo == null || p.EffectiveTo >= start)
        join s in context.PayScaleStages on p.PayScaleStageId equals s.Id
        join v in context.PayScaleVersions on s.PayScaleVersionId equals v.Id
        select new { p.EmployeeId, Slice = new PaySlice(p.PayScaleStageId, s.StageNumber, p.BasicPay, v.MinBasicPay, v.MaxBasicPay, p.EffectiveFrom, p.EffectiveTo) })
      .ToListAsync(cancellationToken);

    // ---- leave without pay ----
    var unpaid = await (
        from a in context.LeaveApplications.AsNoTracking()
        where ids.Contains(a.EmployeeId) && a.Status == LeaveStatus.Approved && a.StartDate <= end && a.EndDate >= start
        join t in context.LeaveTypes on a.LeaveTypeId equals t.Id
        where t.AffectsPayroll
        select new { a.EmployeeId, a.StartDate, a.EndDate })
      .ToListAsync(cancellationToken);

    var overrides = await context.SalaryOverrides.AsNoTracking()
      .Where(o => ids.Contains(o.EmployeeId) && o.EffectiveFrom <= end && (o.EffectiveTo == null || o.EffectiveTo >= start))
      .ToListAsync(cancellationToken);

    var subscriptions = await context.GpFundAccounts.AsNoTracking()
      .Where(a => ids.Contains(a.EmployeeId) && a.Status == RecordStatus.Active && a.OpenedOn <= end)
      .ToDictionaryAsync(a => a.EmployeeId, a => a.MonthlySubscription, cancellationToken);

    // ---- loan installments due, unless another live run already deducts them ----
    var installments = await (
        from i in context.LoanInstallments.AsNoTracking()
        where (i.Status == InstallmentStatus.Pending || i.Status == InstallmentStatus.Partial) && i.DueDate <= end
        join l in context.Loans on i.EmployeeLoanId equals l.Id
        where ids.Contains(l.EmployeeId) && l.Status == LoanStatus.Active
        join t in context.LoanTypes on l.LoanTypeId equals t.Id
        where t.SalaryComponentId != null
          && !context.PayrollLoanDeductions.Any(d => d.InstallmentId == i.Id
            && context.PayrollTransactions.Any(x => x.Id == d.PayrollTransactionId && x.PayrollRunId != run.Id
              && context.PayrollRuns.Any(r => r.Id == x.PayrollRunId && r.Status != PayrollRunStatus.Reversed)))
        select new
        {
          l.EmployeeId,
          Due = new DueInstallment(l.Id, i.Id, t.SalaryComponentId!, l.DeductionPriority, i.InstallmentNumber, i.DueDate, i.Amount - i.PaidAmount)
        })
      .ToListAsync(cancellationToken);

    // ---- tax position ----
    var tax = taxYear is null ? null : await TaxPositionsAsync(run, period, taxYear, ids, cancellationToken);

    return employees.ToDictionary(e => e.Id, e =>
    {
      var adjustments = existing.GetValueOrDefault(e.Id)?.Adjustments ?? [];
      return new PayrollEmployeeInput(
        e.Id,
        e.EmploymentType,
        assignments.Where(a => a.EmployeeId == e.Id).Select(a => new AssignmentSlice(a.PostId, a.EffectiveFrom, a.EffectiveTo)).ToList(),
        versions.Where(v => assignments.Any(a => a.EmployeeId == e.Id && a.PostId == v.PostId)).ToList(),
        pay.Where(p => p.EmployeeId == e.Id).Select(p => p.Slice).ToList(),
        unpaid.Where(u => u.EmployeeId == e.Id).Select(u => new DateRange(u.StartDate, u.EndDate)).ToList(),
        overrides.Where(o => o.EmployeeId == e.Id).ToList(),
        run.IsComputed ? subscriptions.GetValueOrDefault(e.Id) : 0,
        run.IsComputed ? installments.Where(i => i.EmployeeId == e.Id).Select(i => i.Due).ToList() : [],
        adjustments.Where(a => a.Amount > 0).Sum(a => a.Amount),
        adjustments.Sum(a => a.Amount),
        tax?.GetValueOrDefault(e.Id));
    });
  }

  /// Year-to-date taxable income and tax from posted runs of the tax year (this run left out), the exemptions, and -
  /// for runs made of adjustments - a normal month's taxable pay (the last posted regular slip).
  private async Task<Dictionary<EmployeeId, TaxInput>> TaxPositionsAsync(PayrollRun run, PayrollPeriod period, TaxYear year, List<EmployeeId> ids, CancellationToken cancellationToken)
  {
    var ytd = (await (
          from l in context.TaxLedger.AsNoTracking()
          where ids.Contains(l.EmployeeId) && l.TaxYearId == year.Id
          join t in context.PayrollTransactions on l.PayrollTransactionId equals t.Id
          join r in context.PayrollRuns on t.PayrollRunId equals r.Id
          where r.Id != run.Id && Posted.Contains(r.Status)
          select new { l.EmployeeId, l.TaxableIncome, l.TaxWithheld })
        .ToListAsync(cancellationToken))
      .GroupBy(x => x.EmployeeId)
      .ToDictionary(g => g.Key, g => (Taxable: g.Sum(x => x.TaxableIncome), Withheld: g.Sum(x => x.TaxWithheld)));

    var exemptions = (await context.TaxExemptions.AsNoTracking().Where(e => ids.Contains(e.EmployeeId) && e.TaxYearId == year.Id)
        .Select(e => new { e.EmployeeId, e.Amount }).ToListAsync(cancellationToken))
      .GroupBy(e => e.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

    var recurring = run.IsComputed ? new Dictionary<EmployeeId, decimal>() : await RecurringTaxableAsync(ids, cancellationToken);
    var months = year.MonthsRemainingFrom(period.Year, period.Month);

    return ids.ToDictionary(id => id, id =>
    {
      var (taxable, withheld) = ytd.GetValueOrDefault(id);
      return new TaxInput(year, exemptions.GetValueOrDefault(id), taxable, withheld, months, recurring.GetValueOrDefault(id));
    });
  }

  /// A normal month's taxable pay: the taxable earnings of each employee's latest posted regular pay slip.
  private async Task<Dictionary<EmployeeId, decimal>> RecurringTaxableAsync(List<EmployeeId> ids, CancellationToken cancellationToken)
  {
    var latest = await (
        from t in context.PayrollTransactions.AsNoTracking()
        where ids.Contains(t.EmployeeId)
        join r in context.PayrollRuns on t.PayrollRunId equals r.Id
        where r.RunType == PayrollRunType.Regular && Posted.Contains(r.Status)
        join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
        select new { t.Id, t.EmployeeId, p.Year, p.Month })
      .ToListAsync(cancellationToken);
    var slipIds = latest.GroupBy(x => x.EmployeeId).Select(g => g.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).First().Id).ToList();

    var lines = await (
        from l in context.PayrollLines.AsNoTracking()
        where slipIds.Contains(l.PayrollTransactionId) && l.ComponentType == ComponentType.Earning
        join c in context.SalaryComponents on l.SalaryComponentId equals c.Id
        where c.IsTaxable
        join t in context.PayrollTransactions on l.PayrollTransactionId equals t.Id
        select new { t.EmployeeId, l.CalculatedAmount })
      .ToListAsync(cancellationToken);

    return lines.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.CalculatedAmount));
  }
}
