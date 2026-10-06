using Microsoft.EntityFrameworkCore;

public sealed record MyPayslipDto(Guid PayrollTransactionId, Guid PayrollRunId, string Period, int Year, int Month, PayrollRunType RunType, string? RunLabel,
  decimal GrossPay, decimal TotalDeductions, decimal NetPayable, bool IsHeld, DateTime IssuedAt);

public sealed record GetMyPayslipsQueryResult(IReadOnlyList<MyPayslipDto> Payslips);

/// The employee's issued pay slips (runs that were reversed are left out), newest first.
public sealed record GetMyPayslipsQuery(Guid EmployeeId, int? Year) : IQuery<Result<GetMyPayslipsQueryResult>>;

public sealed record MyLeaveBalanceDto(Guid LeaveTypeId, string? LeaveType, decimal EntitledDays, decimal UsedDays, decimal BalanceDays);

public sealed record MyOverviewDto(
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  EmploymentStatus EmploymentStatus,
  Placement? Placement,
  int OpenTasks,
  int OverdueTasks,
  int PendingLeaveApplications,
  int PendingRequests,
  int ReviewsToAcknowledge,
  int EvaluationsToComplete,
  IReadOnlyList<MyLeaveBalanceDto> LeaveBalances,
  MyPayslipDto? LatestPayslip,
  decimal? GpFundBalance,
  int ActiveLoans,
  decimal LoanBalance);

/// The employee portal's home page: where they sit, what is waiting for them and their balances.
public sealed record GetMyOverviewQuery(Guid EmployeeId) : IQuery<Result<MyOverviewDto>>;

public class SelfServiceHandlers(IApplicationDbContext context, HrLookup lookup, IClock clock) :
  IQueryHandler<GetMyPayslipsQuery, Result<GetMyPayslipsQueryResult>>,
  IQueryHandler<GetMyOverviewQuery, Result<MyOverviewDto>>
{
  public async Task<Result<GetMyPayslipsQueryResult>> Handle(GetMyPayslipsQuery query, CancellationToken cancellationToken) =>
      Result<GetMyPayslipsQueryResult>.Success(new(await PayslipsAsync(EmployeeId.Of(query.EmployeeId), query.Year, null, cancellationToken)));

  public async Task<Result<MyOverviewDto>> Handle(GetMyOverviewQuery query, CancellationToken cancellationToken)
  {
    var employee = await context.LoadEmployeeAsync(query.EmployeeId, cancellationToken);
    var id = employee.Id;
    var today = clock.Today;

    var placement = (await lookup.PlacementsAsync([id], today, cancellationToken)).GetValueOrDefault(id.Value);

    var tasks = await context.Tasks.AsNoTracking()
      .Where(t => t.EmployeeId == id && t.Status != EmployeeTaskStatus.Completed && t.Status != EmployeeTaskStatus.Cancelled)
      .Select(t => t.DueDate).ToListAsync(cancellationToken);
    var pendingLeave = await context.LeaveApplications.CountAsync(a => a.EmployeeId == id && a.Status == LeaveStatus.Pending, cancellationToken);
    var pendingRequests = await context.Requests.CountAsync(r => r.EmployeeId == id && r.Status == EmployeeRequestStatus.Pending, cancellationToken);
    var toAcknowledge = await context.PerformanceReviews.CountAsync(r => r.EmployeeId == id && r.Status == ReviewStatus.Submitted, cancellationToken);
    var toEvaluate = await context.PerformanceReviews.CountAsync(r => r.EvaluatorId == id && r.Status == ReviewStatus.Draft, cancellationToken);

    var balances = await context.LeaveBalances.AsNoTracking().Where(b => b.EmployeeId == id.Value && b.Year == today.Year).ToListAsync(cancellationToken);
    var types = await context.LeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, t => t.Name, cancellationToken);

    var account = await context.GpFundAccounts.AsNoTracking().Where(a => a.EmployeeId == id).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken);
    decimal? fund = account is null
      ? null
      : await context.GpFundBalances.AsNoTracking().Where(b => b.GpFundAccountId == account.Value).Select(b => b.Balance).FirstOrDefaultAsync(cancellationToken);
    var loans = await context.Loans.AsNoTracking().Where(l => l.EmployeeId == id && l.Status == LoanStatus.Active)
      .Select(l => l.RemainingBalance).ToListAsync(cancellationToken);

    var latest = (await PayslipsAsync(id, null, 1, cancellationToken)).FirstOrDefault();

    return Result<MyOverviewDto>.Success(new(id.Value, employee.EmployeeNumber, employee.DisplayName, employee.EmploymentStatus, placement,
      tasks.Count, tasks.Count(d => d < today), pendingLeave, pendingRequests, toAcknowledge, toEvaluate,
      balances.OrderBy(b => types.GetValueOrDefault(b.LeaveTypeId))
        .Select(b => new MyLeaveBalanceDto(b.LeaveTypeId, types.GetValueOrDefault(b.LeaveTypeId), b.EntitledDays, b.UsedDays, b.BalanceDays)).ToList(),
      latest, fund, loans.Count, loans.Sum()));
  }

  private async Task<List<MyPayslipDto>> PayslipsAsync(EmployeeId employeeId, int? year, int? take, CancellationToken cancellationToken)
  {
    var rows =
      from s in context.Payslips.AsNoTracking()
      join t in context.PayrollTransactions on s.PayrollTransactionId equals t.Id
      where t.EmployeeId == employeeId
      join r in context.PayrollRuns on t.PayrollRunId equals r.Id
      where r.Status != PayrollRunStatus.Reversed
      join p in context.PayrollPeriods on r.PayrollPeriodId equals p.Id
      where year == null || p.Year == year
      orderby p.Year descending, p.Month descending, s.GeneratedAt descending
      select new MyPayslipDto(t.Id.Value, r.Id.Value, "", p.Year, p.Month, r.RunType, r.RunLabel, t.GrossPay, t.TotalDeductions, t.NetPayable,
        t.Status == PayrollTransactionStatus.Held, s.GeneratedAt);

    var list = take is { } n ? await rows.Take(n).ToListAsync(cancellationToken) : await rows.ToListAsync(cancellationToken);
    return list.Select(p => p with { Period = $"{new DateOnly(p.Year, p.Month, 1):MMMM yyyy}" }).ToList();
  }
}
