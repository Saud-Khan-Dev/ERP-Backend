using static Fixture;

public class HrActionRuleTests
{
  private static readonly PostId Current = PostId.New();
  private static readonly PostId Other = PostId.New();
  private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

  private static HrActionDetails Action(HrActionType type, PostId? post = null, string on = "2026-11-02", string? until = null,
      string? department = null, EmploymentMethodId? method = null) =>
    new(type, post, null, null, D(on), "GDA/HR/1", null, null, until is null ? null : D(until), null, department, method);

  private static HrActionFacts Facts(Employee employee, int heldBps = 11, int targetBps = 14, int seats = 1, int taken = 0,
      bool holds = true, bool target = true, bool scale = true, bool separated = false, bool unpaidType = true, bool payBefore = true) =>
    new(employee, 60,
      holds ? new HeldPost(Current, "POST-0001", heldBps, D("2024-01-01")) : null,
      target ? new TargetPost(Other, "POST-0009", targetBps, PostLifecycle.Sanctioned, seats, taken, scale) : null,
      separated, false, null, unpaidType, null, payBefore, true, true);

  [Fact]
  public void A_promotion_needs_a_higher_grade_when_it_is_drafted()
  {
    var employee = Employee();

    Assert.Contains("higher grade", HrActionRules.Why(Action(HrActionType.Promotion, Other), Facts(employee, heldBps: 11, targetBps: 11)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Promotion, Other), Facts(employee, heldBps: 11, targetBps: 14)));
  }

  [Fact]
  public void A_transfer_stays_in_the_same_grade()
  {
    var employee = Employee();

    Assert.NotNull(HrActionRules.Why(Action(HrActionType.Transfer, Other), Facts(employee, heldBps: 11, targetBps: 14)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Transfer, Other), Facts(employee, heldBps: 14, targetBps: 14)));
  }

  [Fact]
  public void A_move_is_dated_after_the_current_post_started()
  {
    var employee = Employee();

    Assert.Contains("dated after", HrActionRules.Why(Action(HrActionType.Transfer, Other, on: "2024-01-01"), Facts(employee, 14, 14)));
  }

  [Fact]
  public void A_full_post_is_refused_when_the_action_is_drafted()
  {
    var employee = Employee();

    Assert.Contains("full", HrActionRules.Why(Action(HrActionType.Promotion, Other), Facts(employee, seats: 1, taken: 1)));
  }

  [Fact]
  public void A_post_whose_grade_has_no_scale_cannot_take_a_promotion()
  {
    Assert.Contains("No pay scale", HrActionRules.Why(Action(HrActionType.Promotion, Other), Facts(Employee(), scale: false)));
  }

  [Fact]
  public void Reinstatement_brings_back_only_the_suspended_terminated_dismissed_or_removed()
  {
    var active = Employee();
    Assert.NotNull(HrActionRules.Fit(HrActionType.Reinstatement, Facts(active)));

    var suspended = Employee();
    suspended.ChangeEmploymentStatus(EmploymentStatus.Suspended);
    Assert.Null(HrActionRules.Why(Action(HrActionType.Reinstatement), Facts(suspended)));

    var retired = Employee();
    retired.ChangeEmploymentStatus(EmploymentStatus.Retired);
    Assert.NotNull(HrActionRules.Fit(HrActionType.Reinstatement, Facts(retired, separated: true)));

    var dismissed = Employee();
    dismissed.ChangeEmploymentStatus(EmploymentStatus.Dismissed);
    Assert.Contains("Choose the post", HrActionRules.Why(Action(HrActionType.Reinstatement), Facts(dismissed, holds: false, separated: true)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Reinstatement, Other), Facts(dismissed, holds: false, separated: true)));
  }

  [Fact]
  public void A_suspended_employee_is_not_suspended_again_nor_promoted()
  {
    var employee = Employee();
    employee.ChangeEmploymentStatus(EmploymentStatus.Suspended);

    Assert.NotNull(HrActionRules.Fit(HrActionType.Suspension, Facts(employee)));
    Assert.NotNull(HrActionRules.Fit(HrActionType.Promotion, Facts(employee)));
  }

  [Fact]
  public void An_appointment_needs_a_date_of_birth_and_an_age_from_18_to_the_retirement_age()
  {
    var young = Employee(D("2010-01-01"));
    var old = Employee(D("1960-01-01"));
    var fit = Employee(D("1995-01-01"));

    Assert.Contains("at least 18", HrActionRules.Why(Action(HrActionType.Appointment, Other), Facts(young, holds: false)));
    Assert.Contains("retirement age", HrActionRules.Why(Action(HrActionType.Appointment, Other), Facts(old, holds: false)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Appointment, Other), Facts(fit, holds: false)));
  }

  [Fact]
  public void Leave_without_pay_needs_its_last_day_and_a_leave_type_deducted_from_pay()
  {
    var employee = Employee();

    Assert.Contains("last day", HrActionRules.Why(Action(HrActionType.Lwop), Facts(employee, target: false)));
    Assert.Contains("deducted from pay", HrActionRules.Why(Action(HrActionType.Lwop, until: "2026-11-30"), Facts(employee, target: false, unpaidType: false)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Lwop, until: "2026-11-30"), Facts(employee, target: false)));
  }

  [Fact]
  public void A_deputation_names_the_other_department()
  {
    Assert.NotNull(HrActionRules.Why(Action(HrActionType.DeputationOut), Facts(Employee(), target: false)));
    Assert.Null(HrActionRules.Why(Action(HrActionType.DeputationOut, department: "C&W Department"), Facts(Employee(), target: false)));
  }

  [Fact]
  public void Only_a_non_regular_employee_is_regularized()
  {
    var contract = Employee(type: EmploymentBasis.FixedTerm);

    Assert.NotNull(HrActionRules.Fit(HrActionType.Regularization, Facts(Employee())));
    Assert.Null(HrActionRules.Why(Action(HrActionType.Regularization, method: ScheduledSeat), Facts(contract, target: false)));
  }

  [Fact]
  public void A_contract_ends_only_for_contract_staff_and_a_deputation_only_for_deputationists()
  {
    Assert.NotNull(HrActionRules.Fit(HrActionType.EndOfContract, Facts(Employee())));
    Assert.Null(HrActionRules.Fit(HrActionType.EndOfContract, Facts(Employee(type: EmploymentBasis.FixedTerm))));
    Assert.NotNull(HrActionRules.Fit(HrActionType.DeputationEnd, Facts(Employee())));
  }

  [Fact]
  public void The_officer_who_drafted_an_HR_action_cannot_approve_it()
  {
    var employee = Employee();
    var action = HrActionRequest.Draft(HrActionRequestId.New(), employee, Action(HrActionType.Suspension), ServiceChange.None);
    action.CreatedBy = Officer;
    action.Submit(Officer, Now);

    Assert.Throws<DomainException>(() => action.Approve(Officer, Now, null, requireSeparateApprover: true));
    action.Approve(Guid.NewGuid(), Now, null, requireSeparateApprover: true);
    Assert.Equal(HrActionStatus.Approved, action.Status);
  }

  [Fact]
  public void A_status_action_is_applied_on_or_after_its_date_a_dated_move_may_be_applied_ahead()
  {
    var suspension = HrActionRequest.Draft(HrActionRequestId.New(), Employee(), Action(HrActionType.Suspension, on: "2026-11-02"), ServiceChange.None);
    suspension.Submit(Officer, Now);
    suspension.Approve(Guid.NewGuid(), Now, null, true);

    Assert.Throws<DomainException>(() => suspension.EnsureStillCurrent(ServiceChange.None, D("2026-10-20")));
    suspension.EnsureStillCurrent(ServiceChange.None, D("2026-11-02"));
    Assert.False(HrActionRequest.TakesEffectOnItsDate(HrActionType.Transfer));
  }

  [Fact]
  public void Rejecting_or_returning_keeps_who_when_and_why()
  {
    var action = HrActionRequest.Draft(HrActionRequestId.New(), Employee(), Action(HrActionType.Suspension), ServiceChange.None);
    action.Submit(Officer, Now);

    Assert.Throws<DomainException>(() => action.Reject(Officer, Now, null));
    action.ReturnToDraft(Officer, Now, "Attach the inquiry report");
    Assert.Equal(("Attach the inquiry report", Officer), (action.DecisionRemarks, action.DecidedBy!.Value));
  }

  [Fact]
  public void A_separation_leaves_the_real_end_of_service_state()
  {
    Assert.Equal(EmploymentStatus.Dismissed, ServiceEventNames.StatusAfter(SeparationType.Dismissal));
    Assert.Equal(EmploymentStatus.ContractEnded, ServiceEventNames.StatusAfter(SeparationType.EndOfContract));
    Assert.Equal(EmploymentStatus.Repatriated, ServiceEventNames.StatusAfter(SeparationType.DeputationEnd));
    Assert.Equal(EmploymentStatus.Retired, ServiceEventNames.StatusAfter(SeparationType.CompulsoryRetirement));
    Assert.Equal(EmploymentStatus.Removed, ServiceEventNames.StatusAfter(SeparationType.RemovalFromService));
  }

  [Fact]
  public void Holders_who_follow_one_another_share_a_seat()
  {
    var post = PostId.New();
    PositionAssignment Holding(string from, string? to)
    {
      var a = (PositionAssignment)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PositionAssignment));
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.PostId))!.SetValue(a, post);
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.EmployeeId))!.SetValue(a, EmployeeId.New());
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.AssignmentType))!.SetValue(a, AssignmentType.Regular);
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.Status))!.SetValue(a, RecordStatus.Active);
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.EffectiveFrom))!.SetValue(a, D(from));
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.EffectiveTo))!.SetValue(a, to is null ? null : D(to));
      typeof(PositionAssignment).GetProperty(nameof(PositionAssignment.Id))!.SetValue(a, PositionAssignmentId.New());
      return a;
    }

    // one holder until June, the next from July: one seat is enough for a back-dated look over the whole year
    var holdings = new[] { Holding("2025-01-01", "2026-06-30"), Holding("2026-07-01", null) };

    Assert.Equal(1, PostCapacity.MostHeld(holdings, new DateRange(D("2026-01-01"), null)));
    Assert.Equal(2, PostCapacity.MostHeld([.. holdings, Holding("2026-08-01", null)], new DateRange(D("2026-01-01"), null)));
  }

  [Fact]
  public void Leave_without_pay_puts_the_increment_back_by_its_days()
  {
    var stage = PayScaleStageFor(30_000);
    var record = EmployeePayRecord.Open(EmployeePayRecordId.New(), EmployeeId.New(), null, stage, null, D("2025-12-01"), D("2025-12-01"), D("2026-12-01"), "Annual increment", null);
    var lwop = new DateRange(D("2026-03-01"), D("2026-03-30"));

    Assert.True(record.DeferIncrement((from, to) => lwop.Clip(from, to)?.Days ?? 0));
    Assert.Equal((D("2026-12-31"), 30), (record.NextIncrementDate!.Value, record.IncrementDeferredDays));
    // the same leave never defers it twice
    Assert.False(record.DeferIncrement((from, to) => lwop.Clip(from, to)?.Days ?? 0));
  }

  [Fact]
  public void The_first_increment_waits_for_the_qualifying_months_when_the_setting_asks_for_them()
  {
    Assert.Equal(D("2026-12-01"), EmployeePayRecord.NextIncrementAfter(D("2026-11-15"), 12, 1, 0));
    Assert.Equal(D("2027-12-01"), EmployeePayRecord.NextIncrementAfter(D("2026-11-15"), 12, 1, 6));
    Assert.Equal(D("2026-12-01"), EmployeePayRecord.NextIncrementAfter(D("2026-05-15"), 12, 1, 6));
  }

  [Fact]
  public void A_contract_employee_becomes_regular_only_through_a_regularization()
  {
    var contract = Employee(type: EmploymentBasis.FixedTerm);

    var regular = Type(EmploymentBasis.Regular);
    Assert.Throws<DomainException>(() => contract.CorrectEmployment(regular, ScheduledSeat, null));
    contract.Regularize(regular, ScheduledSeat);
    Assert.Equal(EmploymentBasis.Regular, contract.EmploymentBasis);
  }

  [Fact]
  public void A_term_belongs_to_non_regular_staff_and_a_deputationist_names_the_parent_department()
  {
    Assert.Throws<DomainException>(() => Employee().SetTerm(D("2026-01-01"), D("2026-12-31"), null));
    var deputationist = Employee(type: EmploymentBasis.Deputation);
    Assert.Throws<DomainException>(() => deputationist.SetTerm(D("2026-01-01"), D("2028-12-31"), null));
    deputationist.SetTerm(D("2026-01-01"), D("2028-12-31"), "C&W Department");
    Assert.Equal(D("2028-12-31"), deputationist.TermTo);
  }

  [Fact]
  public void A_family_member_takes_a_configured_relation_and_an_event_group_comes_from_a_fixed_list()
  {
    var employee = Employee();
    var son = FamilyRelation.Create(FamilyRelationId.New(), "Son");

    Assert.Throws<DomainException>(() => employee.AddFamilyMember(new FamilyMemberDetails(null, null, null, null, null), D("2026-10-20")));
    Assert.Equal(son.Id, employee.AddFamilyMember(new FamilyMemberDetails(son.Id, "Ali", null, null, null), D("2026-10-20")).RelationId);
    Assert.Throws<DomainException>(() => ServiceEventType.Create(ServiceEventTypeId.New(), "Training", "other"));
  }

  [Fact]
  public void The_change_log_lists_each_changed_personal_field()
  {
    var before = new PersonalDetails("Ali", null, "Khan", "35202-1234567-1", D("1980-01-01"), Gender.Male, "Pakistani", null, null);
    var after = before with { DateOfBirth = D("1981-01-01"), LastName = "Khan Jadoon" };

    var changes = EmployeeChangeLogEntry.Diff(before, after);

    Assert.Equal([EmployeeFields.LastName, EmployeeFields.DateOfBirth], changes.Select(c => c.Field));
  }

  private static PayScaleStage PayScaleStageFor(decimal basicPay)
  {
    var grade = PayScaleGrade.Create(PayScaleGradeId.New(), 11, "BPS-11");
    var scale = PayScaleVersion.Create(PayScaleVersionId.New(), grade, basicPay, basicPay + 1_000, null, "BPS-11 (2026)", null, D("2025-07-01"), null,
      [new PayScaleStageInput(0, basicPay), new PayScaleStageInput(1, basicPay + 1_000)], Officer);
    return scale.Stage(0)!;
  }
}
