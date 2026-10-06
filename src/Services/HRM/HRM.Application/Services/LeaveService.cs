using Microsoft.EntityFrameworkCore;

/// Leave days and balances. A balance is the year's entitlement plus the signed ledger; what is still available also
/// subtracts leave applied for and not yet decided.
public class LeaveService(IApplicationDbContext context, WorkCalendar calendar)
{
  /// The days to charge for a range: the employee's working days that are not holidays, split by calendar year. A
  /// given number of days (e.g. a half day) is spread over the years in proportion.
  public async Task<IReadOnlyList<LeaveDaysByYear>> DaysAsync(EmployeeId employeeId, DateOnly from, DateOnly to, decimal? days, CancellationToken cancellationToken)
  {
    Guard.DateOrder(from, to, "Start date", "End date");
    var employeeCalendar = await calendar.ForAsync(employeeId, from, to, cancellationToken);
    var counted = LeaveDayCounter.CountByYear(from, to, employeeCalendar.IsWorkingDay, employeeCalendar.IsHoliday);
    return days is { } given ? LeaveDayCounter.Split(given, counted, from) : counted;
  }

  public sealed record Balance(int Year, decimal Entitled, decimal Ledger, decimal Pending, bool HasEntitlement)
  {
    public decimal Available => Entitled + Ledger - Pending;
  }

  public async Task<Balance> BalanceAsync(EmployeeId employeeId, LeaveTypeId typeId, int year, LeaveApplicationId? except, CancellationToken cancellationToken)
  {
    var entitlement = await context.LeaveEntitlements.AsNoTracking()
      .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.LeaveTypeId == typeId && e.Year == year, cancellationToken);
    var ledger = await context.LeaveLedger.Where(l => l.EmployeeId == employeeId && l.LeaveTypeId == typeId && l.Year == year)
      .SumAsync(l => (decimal?)l.Days, cancellationToken) ?? 0;
    var pending = await context.LeaveApplications
      .Where(a => a.EmployeeId == employeeId && a.LeaveTypeId == typeId && a.Status == LeaveStatus.Pending && a.StartDate.Year == year && a.Id != except)
      .SumAsync(a => (decimal?)a.Days, cancellationToken) ?? 0;
    return new Balance(year, entitlement?.EntitledDays ?? 0, ledger, pending, entitlement is not null);
  }

  /// Leave drawn from a yearly balance needs that year's entitlement and enough days left in it.
  public async Task EnsureAvailableAsync(EmployeeId employeeId, LeaveType type, IReadOnlyList<LeaveDaysByYear> days, LeaveApplicationId? except, CancellationToken cancellationToken)
  {
    foreach (var (year, requested) in days)
    {
      var balance = await BalanceAsync(employeeId, type.Id, year, except, cancellationToken);
      if (!balance.HasEntitlement)
      {
        if (type.IsBalanceTracked)
          throw new DomainException($"There is no {type.Name} entitlement for {year}. Grant the year's entitlement first.");
        continue;
      }

      if (requested > balance.Available)
        throw new DomainException($"Only {balance.Available:0.##} day(s) of {type.Name} are available in {year}; {requested:0.##} asked for.");
    }
  }
}
