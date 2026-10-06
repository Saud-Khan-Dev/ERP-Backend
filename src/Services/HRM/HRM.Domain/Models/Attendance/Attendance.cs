/// A working pattern: hours, grace for late arrival, and the working weekdays (ISO: 1 = Monday ... 7 = Sunday).
/// A shift that ends before it starts runs past midnight.
public class WorkShift : Aggregate<WorkShiftId>
{
  public static readonly short[] DefaultWeekdays = [1, 2, 3, 4, 5];

  public string Name { get; private set; } = default!;
  public TimeOnly StartTime { get; private set; }
  public TimeOnly EndTime { get; private set; }
  public int GraceMinutes { get; private set; }
  public short[] WorkingWeekdays { get; private set; } = DefaultWeekdays;
  public bool IsActive { get; private set; }

  public bool IsOvernight => EndTime <= StartTime;

  /// Scheduled length of the shift in hours.
  public decimal ScheduledHours => (decimal)Span(StartTime, EndTime).TotalHours;

  public static WorkShift Create(WorkShiftId id, string name, TimeOnly startTime, TimeOnly endTime, int graceMinutes, IReadOnlyCollection<int>? workingWeekdays)
  {
    var shift = new WorkShift { Id = id, IsActive = true };
    shift.Update(name, startTime, endTime, graceMinutes, workingWeekdays);
    return shift;
  }

  public void Update(string name, TimeOnly startTime, TimeOnly endTime, int graceMinutes, IReadOnlyCollection<int>? workingWeekdays)
  {
    if (startTime == endTime)
      throw new DomainException("A shift cannot start and end at the same time.");

    Name = Guard.RequiredText(name, 100, "Shift name");
    StartTime = startTime;
    EndTime = endTime;
    GraceMinutes = Guard.Between(graceMinutes, 0, 240, "Grace minutes");

    var days = (workingWeekdays is null || workingWeekdays.Count == 0 ? DefaultWeekdays.Select(d => (int)d) : workingWeekdays)
      .Distinct().OrderBy(d => d).ToArray();
    if (days.Any(d => d is < 1 or > 7))
      throw new DomainException("Working weekdays are 1 (Monday) to 7 (Sunday).");
    WorkingWeekdays = days.Select(d => (short)d).ToArray();
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Shift '{Name}' is inactive.");
  }

  public bool IsWorkingDay(DateOnly date) => WorkingWeekdays.Contains(IsoWeekday(date));

  public static short IsoWeekday(DateOnly date) => (short)(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);

  /// Time between two clock times, past midnight when the second is not after the first.
  public static TimeSpan Span(TimeOnly from, TimeOnly to)
  {
    var span = to.ToTimeSpan() - from.ToTimeSpan();
    return span > TimeSpan.Zero ? span : span + TimeSpan.FromHours(24);
  }
}

/// Which shift an employee works, from when. Periods never overlap; a new assignment closes the open one.
public class EmployeeShift : Aggregate<EmployeeShiftId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public WorkShiftId WorkShiftId { get; private set; } = default!;
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  /// `current` = the employee's shift assignment that is open on the start date (closed the day before), if any.
  public static EmployeeShift Assign(EmployeeShiftId id, Employee employee, WorkShift shift, DateOnly effectiveFrom, DateOnly? effectiveTo, IReadOnlyCollection<EmployeeShift> existing)
  {
    ArgumentNullException.ThrowIfNull(employee);
    ArgumentNullException.ThrowIfNull(shift);
    employee.EnsureInService();
    shift.EnsureActive();
    DateRange.EnsureValid(effectiveFrom, effectiveTo);

    var range = new DateRange(effectiveFrom, effectiveTo);

    // an open assignment that started earlier is closed the day before; anything else overlapping is a conflict
    foreach (var other in existing.Where(e => e.EmployeeId == employee.Id && e.Range.Overlaps(range)))
    {
      if (other.EffectiveTo is null && other.EffectiveFrom < effectiveFrom)
        other.EffectiveTo = effectiveFrom.AddDays(-1);
      else
        throw new DomainException($"{employee.DisplayName} already has a shift from {other.EffectiveFrom:yyyy-MM-dd}{(other.EffectiveTo is { } end ? $" to {end:yyyy-MM-dd}" : string.Empty)}.");
    }

    return new EmployeeShift
    {
      Id = id,
      EmployeeId = employee.Id,
      WorkShiftId = shift.Id,
      EffectiveFrom = effectiveFrom,
      EffectiveTo = effectiveTo
    };
  }

  public void EndOn(DateOnly lastDay)
  {
    DateRange.EnsureValid(EffectiveFrom, lastDay);
    EffectiveTo = lastDay;
  }
}

/// A holiday on a date; LocationId null = everywhere (gazetted holidays), otherwise only at that location.
public class Holiday : Aggregate<HolidayId>
{
  public DateOnly HolidayDate { get; private set; }
  public string Name { get; private set; } = default!;
  public HolidayType HolidayType { get; private set; }
  public LocationId? LocationId { get; private set; }
  public string? NotificationRef { get; private set; }

  public static Holiday Create(HolidayId id, DateOnly date, string name, HolidayType type, LocationId? locationId, string? notificationRef)
  {
    var holiday = new Holiday { Id = id };
    holiday.Update(date, name, type, locationId, notificationRef);
    return holiday;
  }

  public void Update(DateOnly date, string name, HolidayType type, LocationId? locationId, string? notificationRef)
  {
    HolidayDate = date;
    Name = Guard.RequiredText(name, 150, "Holiday name");
    HolidayType = type;
    LocationId = locationId;
    NotificationRef = Guard.Text(notificationRef, 200, "Notification");
  }

  public bool AppliesTo(LocationId? location) => LocationId is null || LocationId == location;
}

/// One employee's attendance on one date. Hours, lateness and early departure are worked out from the times and the
/// shift; the status is derived when it is not given (absent without a check-in, half day under half the shift,
/// late after the grace period).
public class AttendanceRecord : Aggregate<AttendanceRecordId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public DateOnly AttendanceDate { get; private set; }
  public WorkShiftId? WorkShiftId { get; private set; }
  public TimeOnly? CheckIn { get; private set; }
  public TimeOnly? CheckOut { get; private set; }
  public decimal? WorkingHours { get; private set; }
  public decimal? OvertimeHours { get; private set; }
  public int LateMinutes { get; private set; }
  public int EarlyDepartureMinutes { get; private set; }
  public AttendanceStatus? Status { get; private set; }
  public string? Remarks { get; private set; }

  public static AttendanceRecord Record(
      AttendanceRecordId id,
      EmployeeId employeeId,
      DateOnly date,
      WorkShift? shift,
      TimeOnly? checkIn,
      TimeOnly? checkOut,
      AttendanceStatus? status,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(employeeId);

    var record = new AttendanceRecord { Id = id, EmployeeId = employeeId, AttendanceDate = date };
    record.Apply(shift, checkIn, checkOut, status, remarks);
    return record;
  }

  public void Update(WorkShift? shift, TimeOnly? checkIn, TimeOnly? checkOut, AttendanceStatus? status, string? remarks) =>
      Apply(shift, checkIn, checkOut, status, remarks);

  /// Attendance is kept for days that have come; leave and holidays ahead are planned elsewhere.
  public static void EnsureRecordable(DateOnly date, DateOnly today)
  {
    if (date > today)
      throw new DomainException($"{date:yyyy-MM-dd} has not come yet; attendance is recorded for today or earlier.");
  }

  /// A day closed as absent that approved leave covers becomes leave.
  public void MarkOnLeave(string leaveTypeName)
  {
    if (CheckIn is not null || Status != AttendanceStatus.Absent)
      return;

    Status = AttendanceStatus.OnLeave;
    Remarks = Guard.Text($"On approved {leaveTypeName}.", 2000, "Remarks");
  }

  /// A day closed as leave whose leave was cancelled becomes an absence.
  public void ClearLeave()
  {
    if (Status != AttendanceStatus.OnLeave)
      return;

    Status = AttendanceStatus.Absent;
    Remarks = "Leave cancelled.";
  }

  private void Apply(WorkShift? shift, TimeOnly? checkIn, TimeOnly? checkOut, AttendanceStatus? status, string? remarks)
  {
    if (checkOut.HasValue && !checkIn.HasValue)
      throw new DomainException("A check-out needs a check-in.");

    if (checkIn.HasValue && status is (AttendanceStatus.Absent or AttendanceStatus.Holiday or AttendanceStatus.Weekend or AttendanceStatus.OnLeave))
      throw new DomainException($"A day marked {EnumText.Words(status!.Value)} cannot have a check-in.");

    WorkShiftId = shift?.Id;
    CheckIn = checkIn;
    CheckOut = checkOut;
    Remarks = Guard.Text(remarks, 2000, "Remarks");

    WorkingHours = null;
    OvertimeHours = null;
    LateMinutes = 0;
    EarlyDepartureMinutes = 0;

    if (checkIn is { } inTime && checkOut is { } outTime)
    {
      // TimeOnly subtraction wraps at midnight, so work on time-of-day spans
      var worked = outTime.ToTimeSpan() - inTime.ToTimeSpan();
      if (worked <= TimeSpan.Zero && shift is { IsOvernight: true })
        worked += TimeSpan.FromHours(24);
      if (worked <= TimeSpan.Zero)
        throw new DomainException("Check-out must be after check-in.");

      WorkingHours = decimal.Round((decimal)worked.TotalHours, 2);
      if (shift is not null)
        OvertimeHours = decimal.Round(Math.Max(0, WorkingHours.Value - shift.ScheduledHours), 2);
    }

    if (shift is not null && checkIn is { } arrived)
    {
      var lateBy = MinutesAfter(shift.StartTime, arrived, shift.IsOvernight);
      LateMinutes = lateBy > shift.GraceMinutes ? lateBy : 0;
    }

    if (shift is not null && checkOut is { } left)
      EarlyDepartureMinutes = MinutesAfter(left, shift.EndTime, shift.IsOvernight);

    Status = status ?? Derive(shift);
  }

  private AttendanceStatus Derive(WorkShift? shift)
  {
    if (CheckIn is null)
      return AttendanceStatus.Absent;

    if (shift is not null && WorkingHours is { } hours && hours < shift.ScheduledHours / 2)
      return AttendanceStatus.HalfDay;

    return LateMinutes > 0 ? AttendanceStatus.Late : AttendanceStatus.Present;
  }

  /// Whole minutes `later` is after `earlier` (0 when it is not after). On an overnight shift the two times may sit on
  /// either side of midnight, so the difference is taken the short way round.
  private static int MinutesAfter(TimeOnly earlier, TimeOnly later, bool overnight)
  {
    var difference = later.ToTimeSpan() - earlier.ToTimeSpan();
    if (overnight)
    {
      if (difference < TimeSpan.FromHours(-12))
        difference += TimeSpan.FromHours(24);
      else if (difference > TimeSpan.FromHours(12))
        difference -= TimeSpan.FromHours(24);
    }

    return difference > TimeSpan.Zero ? (int)difference.TotalMinutes : 0;
  }
}
