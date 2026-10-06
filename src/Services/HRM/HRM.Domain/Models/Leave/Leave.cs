/// A kind of leave. affects_payroll = the days are deducted from pay (leave without pay).
public class LeaveType : Aggregate<LeaveTypeId>
{
  public string Name { get; private set; } = default!;
  public bool IsPaid { get; private set; }
  public decimal? MaxDaysPerYear { get; private set; }
  public string? AccrualRule { get; private set; }
  public bool CarryForwardAllowed { get; private set; }
  public bool AffectsPayroll { get; private set; }

  /// Leave that is drawn from a yearly balance (an entitlement must exist before it can be applied for).
  public bool IsBalanceTracked => MaxDaysPerYear.HasValue || CarryForwardAllowed;

  public static LeaveType Create(LeaveTypeId id, string name, bool isPaid, decimal? maxDaysPerYear, string? accrualRule, bool carryForwardAllowed, bool affectsPayroll)
  {
    var type = new LeaveType { Id = id };
    type.Update(name, isPaid, maxDaysPerYear, accrualRule, carryForwardAllowed, affectsPayroll);
    return type;
  }

  public void Update(string name, bool isPaid, decimal? maxDaysPerYear, string? accrualRule, bool carryForwardAllowed, bool affectsPayroll)
  {
    if (isPaid && affectsPayroll)
      throw new DomainException("Paid leave cannot also be deducted from pay.");

    Name = Guard.RequiredText(name, 100, "Leave type name");
    IsPaid = isPaid;
    MaxDaysPerYear = Guard.Between(maxDaysPerYear, 0, 366, "Days per year");
    AccrualRule = Guard.Text(accrualRule, 1000, "Accrual rule");
    CarryForwardAllowed = carryForwardAllowed;
    AffectsPayroll = affectsPayroll;
  }
}

/// Days of a leave type an employee is entitled to in a calendar year. Balances are computed from this plus the
/// leave ledger (v_leave_balance), never stored.
public class LeaveEntitlement : Aggregate<LeaveEntitlementId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public LeaveTypeId LeaveTypeId { get; private set; } = default!;
  public int Year { get; private set; }
  public decimal EntitledDays { get; private set; }

  public static LeaveEntitlement Create(LeaveEntitlementId id, EmployeeId employeeId, LeaveType leaveType, int year, decimal entitledDays)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(leaveType);

    var entitlement = new LeaveEntitlement
    {
      Id = id,
      EmployeeId = employeeId,
      LeaveTypeId = leaveType.Id,
      Year = Guard.Between(year, 2000, 2100, "Year")
    };
    entitlement.ChangeDays(entitledDays, ledgerTotal: 0);
    return entitlement;
  }

  /// `ledgerTotal` = the signed total of the year's ledger rows; the balance may not go below zero.
  public void ChangeDays(decimal entitledDays, decimal ledgerTotal)
  {
    var days = decimal.Round(Guard.Between(entitledDays, 0, 366, "Entitled days"), 2);
    if (days + ledgerTotal < 0)
      throw new DomainException($"{-ledgerTotal:0.##} day(s) are already used; the entitlement cannot be lower than that.");

    EntitledDays = days;
  }
}

/// Days of an application that fall in each calendar year (a leave across 31 December is drawn from both years).
public sealed record LeaveDaysByYear(int Year, decimal Days);

/// An application for leave: pending -> approved / rejected, or cancelled by the employee or HR. Approving writes the
/// usage rows to the ledger; cancelling an approved leave writes the reversal rows.
public class LeaveApplication : Aggregate<LeaveApplicationId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public LeaveTypeId LeaveTypeId { get; private set; } = default!;
  public DateOnly StartDate { get; private set; }
  public DateOnly EndDate { get; private set; }
  public decimal Days { get; private set; }
  public LeaveStatus Status { get; private set; }
  public DateOnly AppliedDate { get; private set; }
  public Guid? ApprovedBy { get; private set; }
  public DateOnly? ApprovalDate { get; private set; }
  public string? Reason { get; private set; }

  public DateRange Range => new(StartDate, EndDate);

  public bool IsLive => Status is LeaveStatus.Pending or LeaveStatus.Approved;

  /// `others` = the employee's other live (pending or approved) applications.
  public static LeaveApplication Apply(
      LeaveApplicationId id,
      Employee employee,
      LeaveType leaveType,
      DateOnly startDate,
      DateOnly endDate,
      decimal days,
      string? reason,
      DateOnly appliedDate,
      IReadOnlyCollection<LeaveApplication> others)
  {
    ArgumentNullException.ThrowIfNull(employee);
    ArgumentNullException.ThrowIfNull(leaveType);
    employee.EnsureInService();

    var application = new LeaveApplication
    {
      Id = id,
      EmployeeId = employee.Id,
      LeaveTypeId = leaveType.Id,
      Status = LeaveStatus.Pending,
      AppliedDate = appliedDate
    };
    application.SetPeriod(startDate, endDate, days, reason, others);
    return application;
  }

  public void Change(DateOnly startDate, DateOnly endDate, decimal days, string? reason, IReadOnlyCollection<LeaveApplication> others)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be changed.");

    SetPeriod(startDate, endDate, days, reason, others);
  }

  /// Approves and returns the usage rows to add to the ledger (one per calendar year the leave falls in).
  public IReadOnlyList<LeaveLedgerEntry> Approve(Guid? approvedBy, DateOnly approvalDate, IReadOnlyList<LeaveDaysByYear> daysByYear)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be approved.");

    if (daysByYear.Sum(d => d.Days) != Days)
      throw new DomainException("The days by year do not add up to the days applied for.");

    ApprovedBy = Guard.Actor(approvedBy, "approve leave");
    ApprovalDate = approvalDate;
    Status = LeaveStatus.Approved;

    return daysByYear
      .Where(d => d.Days > 0)
      .Select(d => LeaveLedgerEntry.Usage(EmployeeId, LeaveTypeId, d.Year, d.Days, Id, approvalDate))
      .ToList();
  }

  public void Reject()
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be rejected.");

    Status = LeaveStatus.Rejected;
  }

  /// Cancels a pending or approved application. For an approved one, `usage` = its usage rows in the ledger, which are
  /// reversed (the returned rows give the days back).
  public IReadOnlyList<LeaveLedgerEntry> Cancel(IReadOnlyCollection<LeaveLedgerEntry> usage, DateOnly cancelledOn)
  {
    if (!IsLive)
      throw new DomainException($"The application is already {EnumText.Words(Status)}.");

    var wasApproved = Status == LeaveStatus.Approved;
    Status = LeaveStatus.Cancelled;

    if (!wasApproved)
      return [];

    // net usage per year still standing (a partial earlier reversal is respected)
    return usage
      .Where(e => e.LeaveApplicationId == Id)
      .GroupBy(e => e.Year)
      .Select(g => (Year: g.Key, Used: -g.Sum(e => e.Days)))
      .Where(x => x.Used > 0)
      .Select(x => LeaveLedgerEntry.UsageReversal(EmployeeId, LeaveTypeId, x.Year, x.Used, Id, cancelledOn))
      .ToList();
  }

  private void SetPeriod(DateOnly startDate, DateOnly endDate, decimal days, string? reason, IReadOnlyCollection<LeaveApplication> others)
  {
    Guard.DateOrder(startDate, endDate, "Start date", "End date");

    var calendarDays = endDate.DayNumber - startDate.DayNumber + 1;
    days = decimal.Round(days, 2);
    if (days <= 0)
      throw new DomainException("The leave must be for more than zero days (are all the days holidays or off days?).");
    if (days > calendarDays)
      throw new DomainException($"{days:0.##} day(s) do not fit between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}.");
    if (days % 0.5m != 0)
      throw new DomainException("Leave is taken in whole or half days.");

    var range = new DateRange(startDate, endDate);
    if (others.FirstOrDefault(o => o.Id != Id && o.EmployeeId == EmployeeId && o.IsLive && o.Range.Overlaps(range)) is { } clash)
      throw new DomainException($"This overlaps another {EnumText.Words(clash.Status)} application from {clash.StartDate:yyyy-MM-dd} to {clash.EndDate:yyyy-MM-dd}.");

    StartDate = startDate;
    EndDate = endDate;
    Days = days;
    Reason = Guard.Text(reason, 2000, "Reason");
  }
}

/// Append-only leave transaction. Signed days: opening, accrual, carry-forward and usage reversal add (+); usage,
/// lapse and encashment take away (-); an adjustment goes either way. Rows are never changed or deleted (a database
/// trigger refuses it): a mistake is corrected with an adjustment.
public class LeaveLedgerEntry : Aggregate<LeaveLedgerEntryId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public LeaveTypeId LeaveTypeId { get; private set; } = default!;
  public int Year { get; private set; }
  public LeaveTransactionType TransactionType { get; private set; }
  public decimal Days { get; private set; }
  public DateOnly TransactionDate { get; private set; }
  public LeaveApplicationId? LeaveApplicationId { get; private set; }
  public string? Remarks { get; private set; }

  public static LeaveLedgerEntry Opening(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.Opening, Guard.Positive(days, "Opening days"), date, null, remarks);

  public static LeaveLedgerEntry Accrual(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.Accrual, Guard.Positive(days, "Accrued days"), date, null, remarks);

  public static LeaveLedgerEntry CarryForward(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.CarryForward, Guard.Positive(days, "Carried days"), date, null, remarks);

  public static LeaveLedgerEntry Lapse(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.Lapse, -Guard.Positive(days, "Lapsed days"), date, null, remarks);

  public static LeaveLedgerEntry Encashment(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.Encashment, -Guard.Positive(days, "Encashed days"), date, null, remarks);

  /// Signed: positive gives days, negative takes them.
  public static LeaveLedgerEntry Adjustment(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, DateOnly date, string? remarks)
  {
    if (days == 0)
      throw new DomainException("An adjustment of zero days changes nothing.");
    if (string.IsNullOrWhiteSpace(remarks))
      throw new DomainException("Say why the balance is adjusted.");

    return Create(employeeId, leaveTypeId, year, LeaveTransactionType.Adjustment, days, date, null, remarks);
  }

  internal static LeaveLedgerEntry Usage(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, LeaveApplicationId applicationId, DateOnly date) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.Usage, -Guard.Positive(days, "Used days"), date, applicationId, null);

  internal static LeaveLedgerEntry UsageReversal(EmployeeId employeeId, LeaveTypeId leaveTypeId, int year, decimal days, LeaveApplicationId applicationId, DateOnly date) =>
      Create(employeeId, leaveTypeId, year, LeaveTransactionType.UsageReversal, Guard.Positive(days, "Returned days"), date, applicationId, "Leave cancelled");

  private static LeaveLedgerEntry Create(
      EmployeeId employeeId,
      LeaveTypeId leaveTypeId,
      int year,
      LeaveTransactionType type,
      decimal days,
      DateOnly date,
      LeaveApplicationId? applicationId,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(leaveTypeId);

    return new LeaveLedgerEntry
    {
      Id = LeaveLedgerEntryId.New(),
      EmployeeId = employeeId,
      LeaveTypeId = leaveTypeId,
      Year = Guard.Between(year, 2000, 2100, "Year"),
      TransactionType = type,
      Days = decimal.Round(days, 2),
      TransactionDate = date,
      LeaveApplicationId = applicationId,
      Remarks = Guard.Text(remarks, 2000, "Remarks")
    };
  }
}

/// Counts the leave days in a range: the employee's working days (their shift's weekdays) minus holidays. Without a
/// shift every day counts except holidays.
public static class LeaveDayCounter
{
  public static IReadOnlyList<LeaveDaysByYear> CountByYear(DateOnly from, DateOnly to, Func<DateOnly, bool> isWorkingDay, Func<DateOnly, bool> isHoliday)
  {
    var byYear = new SortedDictionary<int, decimal>();
    for (var day = from; day <= to; day = day.AddDays(1))
    {
      if (!isWorkingDay(day) || isHoliday(day))
        continue;

      byYear[day.Year] = byYear.GetValueOrDefault(day.Year) + 1;
    }

    return byYear.Select(kv => new LeaveDaysByYear(kv.Key, kv.Value)).ToList();
  }

  /// Splits a given number of days (e.g. a half day) across the years of the range in proportion to the counted days.
  public static IReadOnlyList<LeaveDaysByYear> Split(decimal days, IReadOnlyList<LeaveDaysByYear> counted, DateOnly from)
  {
    var total = counted.Sum(c => c.Days);
    if (counted.Count <= 1 || total == 0)
      return [new LeaveDaysByYear(counted.Count == 1 ? counted[0].Year : from.Year, days)];

    if (total == days)
      return counted;

    // proportional, in half days, the last year taking the remainder
    var result = new List<LeaveDaysByYear>();
    var remaining = days;
    for (var i = 0; i < counted.Count; i++)
    {
      var share = i == counted.Count - 1 ? remaining : Math.Min(remaining, Math.Round(days * counted[i].Days / total * 2, MidpointRounding.AwayFromZero) / 2);
      if (share > 0)
        result.Add(new LeaveDaysByYear(counted[i].Year, share));
      remaining -= share;
    }

    return result;
  }
}
