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

  private static readonly DateTime Now = new(2026, 10, 6, 9, 0, 0, DateTimeKind.Utc);

  [Fact]
  public void Approving_writes_usage_per_year_and_cancelling_gives_the_days_back()
  {
    var application = Apply(Employee(), "2026-12-30", "2027-01-04", 4);
    var usage = application.Approve(Officer, null, Now, [new LeaveDaysByYear(2026, 2), new LeaveDaysByYear(2027, 2)]);

    Assert.Equal([-2m, -2m], usage.Select(u => u.Days));
    Assert.Equal(LeaveStatus.Approved, application.Status);

    var reversal = application.Cancel(usage.ToList(), Officer, Now.AddDays(1), "Called back to duty");
    Assert.Equal(0, usage.Concat(reversal).Sum(u => u.Days));
    Assert.Equal(LeaveStatus.Cancelled, application.Status);
  }

  [Fact]
  public void Only_a_pending_application_is_decided()
  {
    var application = Apply(Employee(), "2026-10-08", "2026-10-08", 1);
    application.Reject(Officer, null, Now, "Office short-staffed that day");

    Assert.Throws<DomainException>(() => application.Approve(Officer, null, Now, [new LeaveDaysByYear(2026, 1)]));
    Assert.Throws<DomainException>(() => application.Cancel([], Officer, Now, "x"));
  }

  [Fact]
  public void Leave_days_follow_the_dates_a_half_day_off_one_date_at_most()
  {
    var counted = LeaveDayCounter.CountByYear(D("2026-11-02"), D("2026-11-30"), Weekday, _ => false);

    Assert.Equal(21, LeaveDayCounter.Charge(counted, null, D("2026-11-02"), D("2026-11-30")).Sum(d => d.Days));
    Assert.Equal(20.5m, LeaveDayCounter.Charge(counted, 20.5m, D("2026-11-02"), D("2026-11-30")).Sum(d => d.Days));
    Assert.Throws<DomainException>(() => LeaveDayCounter.Charge(counted, 0.5m, D("2026-11-02"), D("2026-11-30")));
    Assert.Throws<DomainException>(() => LeaveDayCounter.Charge(counted, 20, D("2026-11-02"), D("2026-11-30")));
  }

  [Fact]
  public void A_range_of_only_days_off_charges_nothing_and_is_refused()
  {
    var weekend = LeaveDayCounter.CountByYear(D("2026-10-10"), D("2026-10-11"), Weekday, _ => false);

    Assert.Throws<DomainException>(() => LeaveDayCounter.Charge(weekend, null, D("2026-10-10"), D("2026-10-11")));
  }

  [Fact]
  public void A_rejection_keeps_the_reason_the_decider_and_the_date()
  {
    var application = Apply(Employee(), "2026-10-08", "2026-10-08", 1);

    Assert.Throws<DomainException>(() => application.Reject(Officer, null, Now, "  "));
    application.Reject(Officer, null, Now, "Audit week");
    Assert.Equal(("Audit week", Officer, Now), (application.DecisionReason, application.DecidedBy!.Value, application.DecidedAt!.Value));
  }

  [Fact]
  public void Nobody_approves_their_own_leave()
  {
    var employee = Employee();
    var application = Apply(employee, "2026-10-08", "2026-10-08", 1);

    Assert.Throws<DomainException>(() => application.Approve(Officer, employee.Id, Now, [new LeaveDaysByYear(2026, 1)]));
    application.Approve(Officer, Employee().Id, Now, [new LeaveDaysByYear(2026, 1)]);
  }

  [Fact]
  public void Cancelling_an_approved_leave_needs_a_reason()
  {
    var application = Apply(Employee(), "2026-10-08", "2026-10-08", 1);
    var usage = application.Approve(Officer, null, Now, [new LeaveDaysByYear(2026, 1)]);

    Assert.Throws<DomainException>(() => application.Cancel(usage.ToList(), Officer, Now, null));
  }

  [Fact]
  public void An_inactive_leave_type_takes_no_new_application()
  {
    var type = LeaveType.Create(LeaveTypeId.New(), "Study Leave", true, 10, null, false, false);
    type.SetActive(false);

    Assert.Throws<DomainException>(() => LeaveApplication.Apply(LeaveApplicationId.New(), Employee(), type, D("2026-10-08"), D("2026-10-08"), 1, null, D("2026-10-06"), []));
  }

  private static EmployeeDocument Paper(Employee owner) =>
      EmployeeDocument.Create(EmployeeDocumentId.New(), owner.Id, DocumentType.Create(DocumentTypeId.New(), "Medical certificate", false),
        new DocumentDetails(null, null, null), "/hrm-files/x/documents/m.pdf");

  [Fact]
  public void Leave_that_needs_a_paper_is_refused_without_it_and_takes_the_employees_own()
  {
    var medical = LeaveType.Create(LeaveTypeId.New(), "Medical Leave", true, null, null, false, false, requiresDocument: true);
    var employee = Employee();

    var refused = Assert.Throws<DomainException>(() => LeaveApplication.Apply(LeaveApplicationId.New(), employee, medical, D("2026-10-08"), D("2026-10-09"), 2, null, D("2026-10-06"), []));
    Assert.Contains("supporting paper", refused.Message);
    Assert.Throws<DomainException>(() => LeaveApplication.Apply(LeaveApplicationId.New(), employee, medical, D("2026-10-08"), D("2026-10-09"), 2, null, D("2026-10-06"), [], Paper(Employee())));

    var paper = Paper(employee);
    var applied = LeaveApplication.Apply(LeaveApplicationId.New(), employee, medical, D("2026-10-08"), D("2026-10-09"), 2, null, D("2026-10-06"), [], paper);
    Assert.Equal(paper.Id, applied.SupportingDocumentId);
  }

  [Fact]
  public void A_paper_is_optional_where_the_type_does_not_ask_for_one()
  {
    var employee = Employee();
    Assert.Null(Apply(employee, "2026-10-08", "2026-10-08", 1).SupportingDocumentId);
    var paper = Paper(employee);
    Assert.Equal(paper.Id, LeaveApplication.Apply(LeaveApplicationId.New(), employee, Casual, D("2026-10-12"), D("2026-10-12"), 1, null, D("2026-10-06"), [], paper).SupportingDocumentId);
  }

  [Fact]
  public void Granting_the_year_fills_in_a_placeholder_made_for_a_carried_balance()
  {
    var placeholder = LeaveEntitlement.Placeholder(LeaveEntitlementId.New(), Employee().Id, Casual, 2027);
    var granted = LeaveEntitlement.Create(LeaveEntitlementId.New(), Employee().Id, Casual, 2027, 15);

    Assert.True(placeholder.Grant(20, ledgerTotal: 6));
    Assert.Equal((20m, false), (placeholder.EntitledDays, placeholder.IsPlaceholder));
    Assert.False(granted.Grant(20, 0));
    Assert.Equal(15, granted.EntitledDays);
  }

  [Fact]
  public void An_optional_holiday_is_no_day_off_for_everyone()
  {
    var optional = Holiday.Create(HolidayId.New(), D("2026-12-25"), "Christmas", HolidayType.Optional, null, null);
    var gazetted = Holiday.Create(HolidayId.New(), D("2026-12-25"), "Quaid Day", HolidayType.Public, null, null);

    Assert.False(optional.AppliesTo(null));
    Assert.True(gazetted.AppliesTo(null));
  }

  [Fact]
  public void Leave_without_pay_recorded_by_an_HR_action_is_approved_at_once()
  {
    var unpaid = LeaveType.Create(LeaveTypeId.New(), "Leave Without Pay", false, null, null, false, true);
    var action = HrActionRequestId.New();
    var leave = LeaveApplication.RecordForHrAction(LeaveApplicationId.New(), Employee(), unpaid, D("2026-11-02"), D("2026-11-30"), 21, null, action, Officer, Now, []);

    Assert.Equal((LeaveStatus.Approved, action), (leave.Status, leave.HrActionId!));
    Assert.Throws<DomainException>(() => LeaveApplication.RecordForHrAction(LeaveApplicationId.New(), Employee(), Casual, D("2026-11-02"), D("2026-11-30"), 21, null, action, Officer, Now, []));
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
