/// A kind of leave. affects_payroll = the days are deducted from pay (leave without pay).
public class LeaveType : Aggregate<LeaveTypeId>
{
  public string Name { get; private set; } = default!;
  public bool IsPaid { get; private set; }
  public decimal? MaxDaysPerYear { get; private set; }
  public string? AccrualRule { get; private set; }
  public bool CarryForwardAllowed { get; private set; }
  public bool AffectsPayroll { get; private set; }
  /// An inactive type is no longer offered for new applications; old applications keep it.
  public bool IsActive { get; private set; }

  /// Leave that is drawn from a yearly balance (an entitlement must exist before it can be applied for).
  public bool IsBalanceTracked => MaxDaysPerYear.HasValue || CarryForwardAllowed;

  public static LeaveType Create(LeaveTypeId id, string name, bool isPaid, decimal? maxDaysPerYear, string? accrualRule, bool carryForwardAllowed, bool affectsPayroll)
  {
    var type = new LeaveType { Id = id, IsActive = true };
    type.Update(name, isPaid, maxDaysPerYear, accrualRule, carryForwardAllowed, affectsPayroll);
    return type;
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"'{Name}' is no longer in use; choose another leave type.");
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
  /// Made by the system at 0 days to carry a balance (a carry-forward or an opening balance) before the year's leave
  /// was granted. Granting the year fills it in instead of skipping it.
  public bool IsPlaceholder { get; private set; }

  /// The 0-day row a carried or opening balance hangs on until the year's leave is granted.
  public static LeaveEntitlement Placeholder(LeaveEntitlementId id, EmployeeId employeeId, LeaveType leaveType, int year)
  {
    var entitlement = Create(id, employeeId, leaveType, year, 0);
    entitlement.IsPlaceholder = true;
    return entitlement;
  }

  /// Grants the year's days: fills in a placeholder; a granted entitlement is left alone (returns false).
  public bool Grant(decimal entitledDays, decimal ledgerTotal)
  {
    if (!IsPlaceholder)
      return false;

    ChangeDays(entitledDays, ledgerTotal);
    return true;
  }

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
    IsPlaceholder = false;
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
  /// The last decision (approve, reject, cancel): who, when and why. A rejection and a cancellation need the reason.
  public Guid? DecidedBy { get; private set; }
  public DateTime? DecidedAt { get; private set; }
  public string? DecisionReason { get; private set; }
  /// The HR action (leave without pay) that recorded this leave, if any.
  public HrActionRequestId? HrActionId { get; private set; }

  public DateRange Range => new(StartDate, EndDate);

  public bool IsLive => Status is LeaveStatus.Pending or LeaveStatus.Approved;

  /// `countedDays` = the employee's working days in the range (LeaveDayCounter); `days` = what is charged: the counted
  /// days, or half a day less (a half day on one of the dates). `others` = the employee's other live applications.
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
    leaveType.EnsureActive();

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

  /// Leave without pay recorded by an approved HR action: approved at once by the action's approver (it draws no
  /// balance, so it writes no ledger rows - payroll reads the dates).
  public static LeaveApplication RecordForHrAction(
      LeaveApplicationId id,
      Employee employee,
      LeaveType leaveType,
      DateOnly startDate,
      DateOnly endDate,
      decimal days,
      string? reason,
      HrActionRequestId hrActionId,
      Guid? approvedBy,
      DateTime at,
      IReadOnlyCollection<LeaveApplication> others)
  {
    if (!leaveType.AffectsPayroll)
      throw new DomainException($"'{leaveType.Name}' is not deducted from pay; leave without pay needs a type that is.");

    var application = Apply(id, employee, leaveType, startDate, endDate, days, reason, DateOnly.FromDateTime(at), others);
    application.HrActionId = hrActionId;
    application.ApprovedBy = Guard.Actor(approvedBy, "approve leave");
    application.ApprovalDate = DateOnly.FromDateTime(at);
    application.Status = LeaveStatus.Approved;
    application.DecidedBy = application.ApprovedBy;
    application.DecidedAt = at;
    return application;
  }

  public void Change(DateOnly startDate, DateOnly endDate, decimal days, string? reason, IReadOnlyCollection<LeaveApplication> others)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be changed.");

    SetPeriod(startDate, endDate, days, reason, others);
  }

  /// Takes over a pending application of the same leave without pay for an HR action (approved by the action).
  public void ApproveForHrAction(HrActionRequestId hrActionId, Guid? approvedBy, DateTime at)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be approved.");

    HrActionId = hrActionId;
    ApprovedBy = Guard.Actor(approvedBy, "approve leave");
    ApprovalDate = DateOnly.FromDateTime(at);
    Status = LeaveStatus.Approved;
    DecidedBy = ApprovedBy;
    DecidedAt = at;
  }

  /// Approves and returns the usage rows to add to the ledger (one per calendar year the leave falls in).
  /// `approverEmployeeId` = the employee record linked to the approver's account: nobody approves their own leave.
  public IReadOnlyList<LeaveLedgerEntry> Approve(Guid? approvedBy, EmployeeId? approverEmployeeId, DateTime at, IReadOnlyList<LeaveDaysByYear> daysByYear)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be approved.");
    EnsureNotOwn(approverEmployeeId, "approve");

    if (daysByYear.Sum(d => d.Days) != Days)
      throw new DomainException("The days by year do not add up to the days applied for.");

    var approvalDate = DateOnly.FromDateTime(at);
    ApprovedBy = Guard.Actor(approvedBy, "approve leave");
    ApprovalDate = approvalDate;
    Status = LeaveStatus.Approved;
    DecidedBy = ApprovedBy;
    DecidedAt = at;
    DecisionReason = null;

    return daysByYear
      .Where(d => d.Days > 0)
      .Select(d => LeaveLedgerEntry.Usage(EmployeeId, LeaveTypeId, d.Year, d.Days, Id, approvalDate))
      .ToList();
  }

  public void Reject(Guid? rejectedBy, EmployeeId? rejecterEmployeeId, DateTime at, string? reason)
  {
    if (Status != LeaveStatus.Pending)
      throw new DomainException($"The application is {EnumText.Words(Status)}; only a pending one can be rejected.");
    EnsureNotOwn(rejecterEmployeeId, "decide");

    DecisionReason = Guard.Text(reason, 1000, "Reason") ?? throw new DomainException("Give the reason for rejecting the leave; the employee sees it.");
    DecidedBy = Guard.Actor(rejectedBy, "reject leave");
    DecidedAt = at;
    Status = LeaveStatus.Rejected;
  }

  /// Cancels a pending or approved application. For an approved one, `usage` = its usage rows in the ledger, which are
  /// reversed (the returned rows give the days back). Withdrawing one's own pending application needs no reason;
  /// cancelling an approved leave does.
  public IReadOnlyList<LeaveLedgerEntry> Cancel(IReadOnlyCollection<LeaveLedgerEntry> usage, Guid? cancelledBy, DateTime at, string? reason)
  {
    if (!IsLive)
      throw new DomainException($"The application is already {EnumText.Words(Status)}.");

    var wasApproved = Status == LeaveStatus.Approved;
    var why = Guard.Text(reason, 1000, "Reason");
    if (wasApproved && why is null)
      throw new DomainException("Give the reason for cancelling an approved leave.");

    Status = LeaveStatus.Cancelled;
    DecidedBy = cancelledBy;
    DecidedAt = at;
    DecisionReason = why;
    var cancelledOn = DateOnly.FromDateTime(at);

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

  private void EnsureNotOwn(EmployeeId? deciderEmployeeId, string verb)
  {
    if (deciderEmployeeId is not null && deciderEmployeeId == EmployeeId)
      throw new DomainException($"You cannot {verb} your own leave; another officer decides it.");
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

  /// The days a range charges: the counted working days, or half a day less when one of the dates is a half day.
  /// Any other number is refused - the days follow the dates, they are not typed freely.
  public static IReadOnlyList<LeaveDaysByYear> Charge(IReadOnlyList<LeaveDaysByYear> counted, decimal? given, DateOnly from, DateOnly to)
  {
    var total = counted.Sum(c => c.Days);
    if (total <= 0)
      throw new DomainException($"There is no working day between {from:dd MMM yyyy} and {to:dd MMM yyyy} (only holidays and off days).");

    if (given is not { } days || days == total)
      return counted;

    if (days != total - 0.5m)
      throw new DomainException($"{total:0.##} working day(s) fall in these dates; the leave is {total:0.##} day(s), or {total - 0.5m:0.##} with a half day.");

    return Split(days, counted, from);
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
