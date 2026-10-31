using Microsoft.EntityFrameworkCore;

/// Leave days and balances. A balance is the year's entitlement plus the signed ledger; what is still available also
/// subtracts leave applied for and not yet decided.
public class LeaveService(IApplicationDbContext context, WorkCalendar calendar)
{
  /// The employee's working days in a range that are not holidays, by calendar year.
  public async Task<IReadOnlyList<LeaveDaysByYear>> CountAsync(EmployeeId employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
  {
    Guard.DateOrder(from, to, "Start date", "End date");
    var employeeCalendar = await calendar.ForAsync(employeeId, from, to, cancellationToken);
    return LeaveDayCounter.CountByYear(from, to, employeeCalendar.IsWorkingDay, employeeCalendar.IsHoliday);
  }

  /// The days to charge for a range, split by calendar year: the counted working days, or half a day less (a half
  /// day on one date). Any other number is refused.
  public async Task<IReadOnlyList<LeaveDaysByYear>> DaysAsync(EmployeeId employeeId, DateOnly from, DateOnly to, decimal? days, CancellationToken cancellationToken) =>
      LeaveDayCounter.Charge(await CountAsync(employeeId, from, to, cancellationToken), days, from, to);

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

  /// Leave is simple: every leave type with days per year gives them to each person in service for the year, the first
  /// time that year's balance is needed - nobody has to grant the year by hand. A row that exists is never touched.
  /// The caller saves.
  public async Task EnsureEntitledAsync(IReadOnlyCollection<EmployeeId> employeeIds, int year, CancellationToken cancellationToken, LeaveTypeId? onlyType = null)
  {
    if (employeeIds.Count == 0)
      return;
    var types = await context.LeaveTypes.Where(t => t.IsActive && t.MaxDaysPerYear != null && t.MaxDaysPerYear > 0 && (onlyType == null || t.Id == onlyType))
      .ToListAsync(cancellationToken);
    if (types.Count == 0)
      return;
    var inService = EmploymentStatuses.InService;
    var people = await context.Employees.AsNoTracking()
      .Where(e => employeeIds.Contains(e.Id) && e.ProfileStatus == RecordStatus.Active && inService.Contains(e.EmploymentStatus))
      .Select(e => e.Id).ToListAsync(cancellationToken);
    var typeIds = types.Select(t => t.Id).ToList();
    var have = (await context.LeaveEntitlements.AsNoTracking()
        .Where(e => people.Contains(e.EmployeeId) && typeIds.Contains(e.LeaveTypeId) && e.Year == year)
        .Select(e => new { e.EmployeeId, e.LeaveTypeId }).ToListAsync(cancellationToken))
      .Select(x => (x.EmployeeId, x.LeaveTypeId)).ToHashSet();
    // added in this unit of work and not saved yet
    foreach (var pending in context.LeaveEntitlements.Local.Where(e => e.Year == year))
      have.Add((pending.EmployeeId, pending.LeaveTypeId));
    foreach (var person in people)
      foreach (var type in types.Where(t => !have.Contains((person, t.Id))))
        context.LeaveEntitlements.Add(LeaveEntitlement.Create(LeaveEntitlementId.New(), person, type, year, type.MaxDaysPerYear!.Value));
  }

  /// Leave drawn from a yearly balance needs that year's entitlement and enough days left in it.
  public async Task EnsureAvailableAsync(EmployeeId employeeId, LeaveType type, IReadOnlyList<LeaveDaysByYear> days, LeaveApplicationId? except, CancellationToken cancellationToken)
  {
    foreach (var (year, requested) in days)
    {
      await EnsureEntitledAsync([employeeId], year, cancellationToken, type.Id);
      var given = context.LeaveEntitlements.Local.FirstOrDefault(e => e.EmployeeId == employeeId && e.LeaveTypeId == type.Id && e.Year == year);
      var balance = await BalanceAsync(employeeId, type.Id, year, except, cancellationToken);
      // given just now (not saved yet): the balance read from the database does not see it
      if (!balance.HasEntitlement && given is not null)
        balance = balance with { Entitled = given.EntitledDays, HasEntitlement = true };
      if (!balance.HasEntitlement)
      {
        if (type.IsBalanceTracked)
          throw new DomainException($"{type.Name} has no days for {year}: set its days per year in Configuration › Leave.");
        continue;
      }

      if (requested > balance.Available)
        throw new DomainException($"Only {balance.Available:0.##} day(s) of {type.Name} are available in {year}; {requested:0.##} asked for.");
    }
  }
}
