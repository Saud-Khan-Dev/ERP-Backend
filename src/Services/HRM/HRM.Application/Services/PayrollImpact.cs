using Microsoft.EntityFrameworkCore;

/// A posted payroll run that paid the employee on figures a back-dated change has since made wrong.
public sealed record PayrollImpactDto(Guid RunId, string? RunLabel, PayrollRunType RunType, Guid PeriodId, string Period, DateOnly PeriodStart, string Effect);

/// Finds the posted (finalized or paid, not reversed) runs a back-dated change reaches, so the person applying it is
/// told what to recover or pay as arrears.
public class PayrollImpactFinder(IApplicationDbContext context)
{
  public const string Arrears = "Paid on the old figures: work out the arrears for this month.";
  public const string Recovery = "Paid for days that are no longer payable: recover the overpayment from the next pay.";

  /// Posted runs of months ending on or after `from` (and starting on or before `to`) that paid the employee.
  public async Task<IReadOnlyList<PayrollImpactDto>> RunsAsync(EmployeeId employeeId, DateOnly first, DateOnly? last, string effect, CancellationToken cancellationToken)
  {
    var rows = await (
        from t in context.PayrollTransactions.AsNoTracking()
        where t.EmployeeId == employeeId
        join r in context.PayrollRuns.AsNoTracking() on t.PayrollRunId equals r.Id
        where r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Paid
        join p in context.PayrollPeriods.AsNoTracking() on r.PayrollPeriodId equals p.Id
        where p.EndDate >= first && (last == null || p.StartDate <= last)
        orderby p.StartDate
        select new { r.Id, r.RunLabel, r.RunType, PeriodId = p.Id, p.Year, p.Month, p.StartDate })
      .ToListAsync(cancellationToken);

    return rows.Select(r => new PayrollImpactDto(r.Id.Value, r.RunLabel, r.RunType, r.PeriodId.Value,
      new DateOnly(r.Year, r.Month, 1).ToString("MMMM yyyy"), r.StartDate, effect)).ToList();
  }
}
