using static Fixture;

public class PayrollRunTests
{
  private static readonly Guid Preparer = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid Approver = Guid.Parse("33333333-3333-3333-3333-333333333333");

  private static PayrollPeriod October() => PayrollPeriod.Create(PayrollPeriodId.New(), 2026, 10, null, null);

  private static PayrollRun Reviewed(PayrollPeriod period)
  {
    var run = PayrollRun.Create(PayrollRunId.New(), period, PayrollRunType.Regular, null, null, Preparer);
    run.MarkCalculated(Preparer);
    run.Review(Preparer);
    return run;
  }

  [Fact]
  public void A_period_is_the_calendar_month_by_default()
  {
    var period = October();

    Assert.Equal(D("2026-10-01"), period.StartDate);
    Assert.Equal(D("2026-10-31"), period.EndDate);
    Assert.Equal(31, period.Days);
  }

  [Fact]
  public void The_preparer_cannot_approve_their_own_payroll()
  {
    var run = Reviewed(October());

    Assert.Throws<DomainException>(() => run.Approve(Preparer, DateTime.UtcNow, requireSeparateApprover: true));
    run.Approve(Approver, DateTime.UtcNow, requireSeparateApprover: true);
    Assert.Equal(PayrollRunStatus.Approved, run.Status);
  }

  [Fact]
  public void Figures_change_only_before_review_and_steps_follow_the_order()
  {
    var run = Reviewed(October());

    Assert.Throws<DomainException>(() => run.EnsureEditable());
    Assert.Throws<DomainException>(() => run.FinalizeRun(DateTime.UtcNow));
    run.SendBackToCalculated();
    run.EnsureEditable();
  }

  [Fact]
  public void Only_a_finalized_or_paid_run_is_reversed()
  {
    var run = Reviewed(October());
    Assert.Throws<DomainException>(() => run.Reverse());

    run.Approve(Approver, DateTime.UtcNow, true);
    run.FinalizeRun(DateTime.UtcNow);
    run.MarkPaid(DateTime.UtcNow);
    run.Reverse();
    Assert.Equal(PayrollRunStatus.Reversed, run.Status);
  }

  [Fact]
  public void A_month_closes_only_when_its_runs_are_finished_and_a_locked_month_never_reopens()
  {
    var period = October();

    Assert.Throws<DomainException>(() => period.Close(unfinishedRuns: 1));
    period.Close(0);
    period.Lock();
    Assert.Throws<DomainException>(() => period.Reopen());
    Assert.Throws<DomainException>(() => PayrollRun.Create(PayrollRunId.New(), period, PayrollRunType.Supplementary, null, null, Preparer));
  }

  [Fact]
  public void Adjustments_survive_a_recalculation_and_count_on_their_side()
  {
    var run = PayrollRun.Create(PayrollRunId.New(), October(), PayrollRunType.Regular, null, null, Preparer);
    var slip = PayrollTransaction.Open(PayrollTransactionId.New(), run, Employee());
    slip.AddAdjustment(AdjustmentType.Arrears, 10_000, "September increment");
    slip.AddAdjustment(AdjustmentType.Recovery, -2_000, "Overpaid conveyance");
    var basic = Component(SystemComponents.BasicPay);
    var line = new LineResult(0, basic.Id, null, ComponentSource.Rule, ComponentType.Earning, null, null, null, null, 60_000, null, null);
    var segment = new SegmentResult(0, PostId.New(), PayScaleGradeId.New(), null, 60_000, D("2026-10-01"), D("2026-10-31"), 31, 31);

    slip.ApplyCalculation(new PayCalculation(31, [segment], [line], 70_000, 0, []));
    slip.ApplyCalculation(new PayCalculation(31, [segment], [line], 70_000, 0, []));

    Assert.Equal(2, slip.Adjustments.Count);
    Assert.Equal(70_000, slip.GrossPay);
    Assert.Equal(2_000, slip.TotalDeductions);
    Assert.Equal(68_000, slip.NetPayable);
    Assert.Throws<DomainException>(() => slip.AddAdjustment(AdjustmentType.Recovery, 500, "wrong sign"));
  }
}

public class StructureTests
{
  [Fact]
  public void Stages_rise_by_the_increment_and_the_last_is_capped_at_the_maximum()
  {
    var stages = PayScaleVersion.StagesFromIncrement(50_000, 56_000, 2_500);

    Assert.Equal([50_000m, 52_500m, 55_000m, 56_000m], stages.Select(s => s.BasicPay));
    Assert.Equal([0, 1, 2, 3], stages.Select(s => s.StageNumber));
  }

  [Fact]
  public void A_restructuring_closes_the_previous_version_the_day_before()
  {
    var type = OrganizationUnitTypeId.New();
    var unit = OrganizationUnit.Create(OrganizationUnitId.New(), "ENG", new OrganizationUnitDetails("Engineering Wing", type, null, null, null), D("2020-01-01"));

    unit.Restructure(new OrganizationUnitDetails("Engineering Directorate", type, null, null, null), D("2025-07-01"));

    Assert.Equal("Engineering Wing", unit.VersionOn(D("2025-06-30"))!.Name);
    Assert.Equal("Engineering Directorate", unit.VersionOn(D("2025-07-01"))!.Name);
    Assert.Throws<DomainException>(() => unit.Restructure(new OrganizationUnitDetails("Back", type, null, null, null), D("2025-07-01")));
  }

  [Fact]
  public void A_unit_cannot_be_its_own_parent()
  {
    var type = OrganizationUnitTypeId.New();
    var id = OrganizationUnitId.New();

    Assert.Throws<DomainException>(() => OrganizationUnit.Create(id, "X", new OrganizationUnitDetails("X", type, id, null, null), D("2020-01-01")));
  }

  [Fact]
  public void A_post_cannot_drop_below_the_seats_already_filled()
  {
    var details = new PostDetails(DesignationId.New(), PayScaleGradeId.New(), OrganizationUnitId.New(), null, EmploymentType.Regular, null, 3, null);
    var post = Post.Create(PostId.New(), "ENG-AD-01", details, D("2020-01-01"), Officer);

    Assert.Throws<DomainException>(() => post.Revise(details with { SanctionedCount = 1 }, D("2026-01-01"), filledSeats: 2, Officer));
    post.Revise(details with { SanctionedCount = 2 }, D("2026-01-01"), filledSeats: 2, Officer);
    Assert.Equal(3, post.VersionOn(D("2025-12-31"))!.SanctionedCount);
  }
}

public class PerformanceTests
{
  private static PerformanceReview Review()
  {
    var period = PerformancePeriod.Create(PerformancePeriodId.New(), "PER 2025-26", D("2025-07-01"), D("2026-06-30"));
    return PerformanceReview.Create(PerformanceReviewId.New(), Employee(), period, Employee());
  }

  [Fact]
  public void Goal_weights_must_total_100_to_submit()
  {
    var review = Review();
    review.SetContent([new GoalInput("Water scheme", 60, null, null, 85), new GoalInput("Approvals", 30, null, null, 92)], [], []);

    Assert.Throws<DomainException>(() => review.Submit());
    review.SetContent([new GoalInput("Water scheme", 60, null, null, 85), new GoalInput("Approvals", 40, null, null, 92)], [], []);
    review.Submit();
  }

  [Fact]
  public void The_final_score_defaults_to_the_weighted_goal_score_and_its_per_category()
  {
    var review = Review();
    review.SetContent([new GoalInput("Water scheme", 60, null, null, 85), new GoalInput("Approvals", 40, null, null, 92)], [], []);
    review.Submit();

    review.FinalizeReview(null, null);

    Assert.Equal(87.8m, review.FinalScore);
    Assert.Equal("Very Good", review.PerformanceGrade);
  }

  [Fact]
  public void Nobody_evaluates_themselves()
  {
    var employee = Employee();
    var period = PerformancePeriod.Create(PerformancePeriodId.New(), "PER", D("2025-07-01"), D("2026-06-30"));

    Assert.Throws<DomainException>(() => PerformanceReview.Create(PerformanceReviewId.New(), employee, period, employee));
  }
}
