/// Why the pay position changed. Stored as text in employee_pay_record.reason.
public static class PayChangeReasons
{
  public const string Appointment = "Appointment";
  public const string AnnualIncrement = "Annual increment";
  public const string Promotion = "Promotion";
  public const string Demotion = "Demotion";
  public const string PayRevision = "Pay revision";
  public const string Correction = "Correction";
  public const string Reinstatement = "Reinstatement";
  public const string Joining = "Joining";
}

/// Effective-dated pay position of an employee: the pay-scale stage and the basic pay actually drawn. Annual increments,
/// promotions and pay revisions close the current row and open the next one. Payroll reads basic pay from here.
public class EmployeePayRecord : Aggregate<EmployeePayRecordId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public PayScaleStageId PayScaleStageId { get; private set; } = default!;
  public decimal BasicPay { get; private set; }
  public DateOnly? LastIncrementDate { get; private set; }
  public DateOnly? NextIncrementDate { get; private set; }
  /// Days the next increment has already been put back by leave without pay (so the same leave never defers it twice).
  public int IncrementDeferredDays { get; private set; }
  public string? Reason { get; private set; }
  public string? OrderNumber { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public bool IsOpen => EffectiveTo is null;

  /// Opens a new pay position. `current` (the employee's open record, if any) is closed the day before.
  public static EmployeePayRecord Open(
      EmployeePayRecordId id,
      EmployeeId employeeId,
      EmployeePayRecord? current,
      PayScaleStage stage,
      decimal? basicPay,
      DateOnly effectiveFrom,
      DateOnly? lastIncrementDate,
      DateOnly? nextIncrementDate,
      string? reason,
      string? orderNumber)
  {
    ArgumentNullException.ThrowIfNull(employeeId);
    ArgumentNullException.ThrowIfNull(stage);

    if (current is not null)
    {
      if (current.EmployeeId != employeeId)
        throw new DomainException("The current pay record belongs to another employee.");

      if (effectiveFrom <= current.EffectiveFrom)
        throw new DomainException($"The new pay must start after {current.EffectiveFrom:yyyy-MM-dd}, when the current pay started.");

      current.CloseOn(effectiveFrom.AddDays(-1));
    }

    Guard.DateOrder(lastIncrementDate, nextIncrementDate, "Last increment date", "Next increment date");

    return new EmployeePayRecord
    {
      Id = id,
      EmployeeId = employeeId,
      PayScaleStageId = stage.Id,
      BasicPay = Guard.Money(basicPay ?? stage.BasicPay, "Basic pay"),
      EffectiveFrom = effectiveFrom,
      LastIncrementDate = lastIncrementDate,
      NextIncrementDate = nextIncrementDate,
      Reason = Guard.Text(reason, 100, "Reason"),
      OrderNumber = Guard.Text(orderNumber, 100, "Order number")
    };
  }

  /// The pay stops on a date (the employee left service or the record is replaced).
  public void CloseOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }

  public void SetNextIncrementDate(DateOnly? nextIncrementDate)
  {
    Guard.DateOrder(LastIncrementDate, nextIncrementDate, "Last increment date", "Next increment date");
    NextIncrementDate = nextIncrementDate;
    IncrementDeferredDays = 0;
  }

  /// The increment date before any deferral for leave without pay.
  public DateOnly? ScheduledIncrementDate => NextIncrementDate?.AddDays(-IncrementDeferredDays);

  /// Leave without pay does not count towards an increment: the increment falls that many days later. `unpaidDays`
  /// counts the unpaid days from the start of this pay (or the last increment) to the deferred date; returns whether
  /// the date moved.
  public bool DeferIncrement(Func<DateOnly, DateOnly, int> unpaidDays)
  {
    if (ScheduledIncrementDate is not { } scheduled)
      return false;

    var from = LastIncrementDate ?? EffectiveFrom;
    var deferred = IncrementDeferredDays;
    // the deferral may itself run into more unpaid leave: move until the count stops changing
    for (var i = 0; i < 12; i++)
    {
      var days = unpaidDays(from, scheduled.AddDays(deferred - 1));
      if (days <= deferred)
        break;
      deferred = days;
    }

    if (deferred == IncrementDeferredDays)
      return false;

    IncrementDeferredDays = deferred;
    NextIncrementDate = scheduled.AddDays(deferred);
    return true;
  }

  /// Due and not paid on time: the next increment date has passed.
  public bool IsIncrementOverdue(DateOnly today) => IsOpen && NextIncrementDate is { } next && next < today;

  /// The first occurrence of the annual increment date (1 December in KP government service) after a date.
  public static DateOnly NextIncrementAfter(DateOnly date, int month, int day) => NextIncrementAfter(date, month, day, 0);

  /// The first annual increment date after a date that leaves at least `qualifyingMonths` on the new pay (0 = the first
  /// one after the date, today's rule; GDA to confirm whether six months are needed).
  public static DateOnly NextIncrementAfter(DateOnly date, int month, int day, int qualifyingMonths)
  {
    var candidate = new DateOnly(date.Year, month, Math.Min(day, DateTime.DaysInMonth(date.Year, month)));
    while (candidate <= date || candidate < date.AddMonths(qualifyingMonths))
      candidate = new DateOnly(candidate.Year + 1, month, Math.Min(day, DateTime.DaysInMonth(candidate.Year + 1, month)));
    return candidate;
  }
}
