using static Fixture;

public class LeaveTests
{
  private static readonly LeaveType Casual = LeaveType.Create(LeaveTypeId.New(), "Casual Leave", true, 20, null, false, false);

  private static bool Weekday(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

  private static LeaveApplication Apply(Employee employee, string from, string to, decimal days, IReadOnlyCollection<LeaveApplication>? others = null) =>
      LeaveApplication.Apply(LeaveApplicationId.New(), employee, Casual, D(from), D(to), days, null, D("2026-10-06"), others ?? []);

  [Fact]
  public void Leave_days_skip_days_off_and_holidays()
  {
    // Thu 8 - Tue 13 Oct 2026, Friday the 9th a holiday: Thu, Mon, Tue
    var days = LeaveDayCounter.CountByYear(D("2026-10-08"), D("2026-10-13"), Weekday, d => d == D("2026-10-09"));

    Assert.Equal(3, Assert.Single(days).Days);
  }

  [Fact]
  public void Leave_across_the_new_year_is_drawn_from_both_years()
  {
    var days = LeaveDayCounter.CountByYear(D("2026-12-30"), D("2027-01-04"), Weekday, _ => false);

    Assert.Equal([new LeaveDaysByYear(2026, 2), new LeaveDaysByYear(2027, 2)], days);
  }

  [Fact]
  public void A_half_day_is_a_given_number_of_days()
  {
    var counted = LeaveDayCounter.CountByYear(D("2026-10-08"), D("2026-10-08"), Weekday, _ => false);

    Assert.Equal(0.5m, Assert.Single(LeaveDayCounter.Split(0.5m, counted, D("2026-10-08"))).Days);
  }

  [Fact]
  public void Leave_may_not_overlap_another_live_application()
  {
    var employee = Employee();
    var first = Apply(employee, "2026-10-08", "2026-10-13", 3);

    Assert.Throws<DomainException>(() => Apply(employee, "2026-10-13", "2026-10-14", 2, [first]));
    Apply(employee, "2026-10-14", "2026-10-15", 2, [first]);
  }

  [Fact]
  public void Approving_writes_usage_per_year_and_cancelling_gives_the_days_back()
  {
    var application = Apply(Employee(), "2026-12-30", "2027-01-04", 4);
    var usage = application.Approve(Officer, D("2026-10-06"), [new LeaveDaysByYear(2026, 2), new LeaveDaysByYear(2027, 2)]);

    Assert.Equal([-2m, -2m], usage.Select(u => u.Days));
    Assert.Equal(LeaveStatus.Approved, application.Status);

    var reversal = application.Cancel(usage.ToList(), D("2026-10-07"));
    Assert.Equal(0, usage.Concat(reversal).Sum(u => u.Days));
    Assert.Equal(LeaveStatus.Cancelled, application.Status);
  }

  [Fact]
  public void Only_a_pending_application_is_decided()
  {
    var application = Apply(Employee(), "2026-10-08", "2026-10-08", 1);
    application.Reject();

    Assert.Throws<DomainException>(() => application.Approve(Officer, D("2026-10-06"), [new LeaveDaysByYear(2026, 1)]));
    Assert.Throws<DomainException>(() => application.Cancel([], D("2026-10-06")));
  }

  [Fact]
  public void An_entitlement_cannot_drop_below_what_was_used() =>
      Assert.Throws<DomainException>(() => LeaveEntitlement.Create(LeaveEntitlementId.New(), Employee().Id, Casual, 2026, 20).ChangeDays(2, -3));
}

public class AttendanceTests
{
  private static readonly WorkShift Morning = WorkShift.Create(WorkShiftId.New(), "Morning", new TimeOnly(8, 0), new TimeOnly(16, 0), 15, null);
  private static readonly WorkShift Night = WorkShift.Create(WorkShiftId.New(), "Night", new TimeOnly(22, 0), new TimeOnly(6, 0), 10, [1, 2, 3, 4, 5, 6, 7]);

  private static AttendanceRecord Record(WorkShift? shift, string? checkIn, string? checkOut, AttendanceStatus? status = null) =>
      AttendanceRecord.Record(AttendanceRecordId.New(), EmployeeId.New(), D("2026-10-05"), shift,
        checkIn is null ? null : TimeOnly.Parse(checkIn), checkOut is null ? null : TimeOnly.Parse(checkOut), status, null);

  [Fact]
  public void Arriving_after_the_grace_period_is_late()
  {
    Assert.Equal(AttendanceStatus.Present, Record(Morning, "08:15", "16:00").Status);
    var late = Record(Morning, "08:20", "16:00");
    Assert.Equal(AttendanceStatus.Late, late.Status);
    Assert.Equal(20, late.LateMinutes);
  }

  [Fact]
  public void Under_half_the_shift_is_a_half_day()
  {
    var record = Record(Morning, "08:05", "11:00");

    Assert.Equal(AttendanceStatus.HalfDay, record.Status);
    Assert.Equal(300, record.EarlyDepartureMinutes);
  }

  [Fact]
  public void No_check_in_is_an_absence() => Assert.Equal(AttendanceStatus.Absent, Record(Morning, null, null).Status);

  [Fact]
  public void An_overnight_shift_counts_the_hours_across_midnight()
  {
    var record = Record(Night, "22:05", "06:30");

    Assert.Equal(8.42m, record.WorkingHours);
    Assert.Equal(0.42m, record.OvertimeHours);
    Assert.Equal(AttendanceStatus.Present, record.Status);
  }

  [Fact]
  public void A_check_out_needs_a_check_in_and_must_come_after_it()
  {
    Assert.Throws<DomainException>(() => Record(Morning, null, "16:00"));
    Assert.Throws<DomainException>(() => Record(Morning, "09:00", "08:00"));
  }

  [Fact]
  public void Days_ahead_cannot_be_recorded() =>
      Assert.Throws<DomainException>(() => AttendanceRecord.EnsureRecordable(D("2026-10-07"), D("2026-10-06")));

  [Fact]
  public void A_shifts_weekdays_decide_the_working_days()
  {
    Assert.True(Morning.IsWorkingDay(D("2026-10-05")));
    Assert.False(Morning.IsWorkingDay(D("2026-10-04")));
    Assert.True(Night.IsWorkingDay(D("2026-10-04")));
  }
}
