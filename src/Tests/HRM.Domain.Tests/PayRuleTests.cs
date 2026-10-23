using static Fixture;

public class PayRuleTests
{
  private static readonly Guid Preparer = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid Reviewer = Guid.Parse("44444444-4444-4444-4444-444444444444");
  private static readonly Guid Adjuster = Guid.Parse("55555555-5555-5555-5555-555555555555");
  private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
  private static readonly SalaryComponent Deduction = Component("MCA", ComponentType.Deduction, taxable: false);

  private static PayrollRun Reviewed()
  {
    var run = PayrollRun.Create(PayrollRunId.New(), PayrollPeriod.Create(PayrollPeriodId.New(), 2026, 10, null, null), PayrollRunType.Regular, null, null, Preparer);
    run.MarkCalculated(Preparer, Now);
    run.Review(Reviewer);
    return run;
  }

  [Fact]
  public void The_reviewer_and_whoever_entered_an_adjustment_cannot_approve_the_payroll()
  {
    Assert.Throws<DomainException>(() => Reviewed().Approve(Reviewer, Now, true));
    Assert.Throws<DomainException>(() => Reviewed().Approve(Adjuster, Now, true, [Adjuster]));
    var run = Reviewed();
    run.Approve(Officer, Now, true, [Adjuster]);
    Assert.Equal(PayrollRunStatus.Approved, run.Status);
  }

  [Fact]
  public void A_payroll_month_changes_dates_or_goes_only_while_open_without_runs()
  {
    var month = PayrollPeriod.Create(PayrollPeriodId.New(), 2026, 12, null, null);
    month.ChangeDates(new DateOnly(2026, 11, 26), new DateOnly(2026, 12, 25), 0);
    Assert.True(month.Overlaps(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 26)));
    Assert.False(month.Overlaps(new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 25)));
    Assert.Throws<DomainException>(() => month.ChangeDates(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 25), 0));
    Assert.Throws<DomainException>(() => month.ChangeDates(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), 1));
    Assert.Throws<DomainException>(() => month.EnsureUnused(2, "be deleted"));
    month.Close(0);
    Assert.Throws<DomainException>(() => month.EnsureUnused(0, "be deleted"));
  }

  [Fact]
  public void Finalizing_paying_and_reversing_keep_who_and_why()
  {
    var run = Reviewed();
    run.Approve(Officer, Now, true);
    run.FinalizeRun(Now, Officer);
    run.Reverse(Officer, Now, "Wrong month's attendance used");

    Assert.Equal((Officer, Officer, "Wrong month's attendance used"), (run.FinalizedBy!.Value, run.ReversedBy!.Value, run.ReversalReason));
  }

  [Fact]
  public void A_repayment_outside_payroll_needs_its_receipt_and_cannot_be_dated_ahead()
  {
    var loan = Loan(LoanType(Deduction), 12_000, 6, start: "2026-10-01");

    Assert.Throws<DomainException>(() => LoanRepayment.Record(loan, D("2026-10-20"), 2_000, null, null, D("2026-10-21")));
    Assert.Throws<DomainException>(() => LoanRepayment.Record(loan, D("2026-10-25"), 2_000, "CH-1", null, D("2026-10-21")));
    Assert.Equal("CH-1", LoanRepayment.Record(loan, D("2026-10-20"), 2_000, "CH-1", null, D("2026-10-21")).ReceiptNumber);
  }

  [Fact]
  public void Waiving_an_installment_needs_the_reason()
  {
    var loan = Loan(LoanType(Deduction), 12_000, 6, start: "2026-10-01");
    var first = loan.Installments[0];

    Assert.Throws<DomainException>(() => loan.WaiveInstallment(first.Id, " ", Officer, Now));
    loan.WaiveInstallment(first.Id, "Sanctioned by the DG, order 12", Officer, Now);
    Assert.Equal("Sanctioned by the DG, order 12", loan.Installments[0].WaiveReason);
  }

  [Fact]
  public void A_loan_application_waits_with_no_installments_until_it_is_sanctioned()
  {
    var employee = Employee();
    var type = LoanType(Deduction);
    var loan = EmployeeLoan.Apply(EmployeeLoanId.New(), employee, type, 12_000, 0, 6, D("2026-10-01"), 100, D("2026-10-21"));
    Assert.Equal(LoanStatus.Pending, loan.Status);
    Assert.Empty(loan.Installments);
    Assert.Null(loan.ApprovedBy);
    Assert.Equal(2_000, loan.MonthlyInstallment);

    loan.Sanction(employee, type, Officer, D("2026-10-21"));
    Assert.Equal(LoanStatus.Active, loan.Status);
    Assert.Equal(6, loan.Installments.Count);
    Assert.Equal(12_000, loan.RemainingBalance);
    Assert.Equal(Officer, loan.ApprovedBy);
    Assert.Throws<DomainException>(() => loan.Sanction(employee, type, Officer, D("2026-10-21")));
  }

  [Fact]
  public void A_pending_loan_application_can_be_turned_down_with_a_reason()
  {
    var loan = EmployeeLoan.Apply(EmployeeLoanId.New(), Employee(), LoanType(Deduction), 12_000, 0, 6, D("2026-10-01"), 100, D("2026-10-21"));
    loan.Cancel("Not eligible this year", Officer, DateTime.UtcNow);
    Assert.Equal(LoanStatus.Cancelled, loan.Status);
    Assert.Throws<DomainException>(() => loan.Repay(100));
  }

  [Fact]
  public void A_loan_application_sanctioned_after_its_start_month_has_gone_is_refused()
  {
    var employee = Employee();
    var type = LoanType(Deduction);
    var loan = EmployeeLoan.Apply(EmployeeLoanId.New(), employee, type, 12_000, 0, 6, D("2026-10-01"), 100, D("2026-10-21"));
    Assert.Throws<DomainException>(() => loan.Sanction(employee, type, Officer, D("2026-11-02")));
  }

  [Fact]
  public void A_loan_cannot_start_in_a_month_already_gone()
  {
    Assert.Throws<DomainException>(() => EmployeeLoan.Sanction(EmployeeLoanId.New(), Employee(), LoanType(Deduction), 12_000, null, 6, D("2026-08-01"), 100, Officer, D("2026-10-21")));
    EmployeeLoan.Sanction(EmployeeLoanId.New(), Employee(), LoanType(Deduction), 12_000, null, 6, D("2026-10-01"), 100, Officer, D("2026-10-21"));
  }

  [Fact]
  public void Tax_slabs_start_at_zero_and_leave_no_gap()
  {
    Assert.Throws<DomainException>(() => global::TaxYear.Create(TaxYearId.New(), "X", D("2026-07-01"), D("2027-06-30"), [new TaxSlabInput(1, 100, null, 0, 1)]));
    Assert.Throws<DomainException>(() => global::TaxYear.Create(TaxYearId.New(), "X", D("2026-07-01"), D("2027-06-30"),
      [new TaxSlabInput(1, 0, 600_000, 0, 0), new TaxSlabInput(2, 700_000, null, 0, 5)]));
  }

  [Fact]
  public void An_opening_tax_position_needs_its_source_and_cannot_withhold_more_than_earned()
  {
    var year = TaxYear();
    Assert.Throws<DomainException>(() => EmployeeTaxOpening.Create(Employee().Id, year, 500_000, 10_000, null));
    Assert.Throws<DomainException>(() => EmployeeTaxOpening.Create(Employee().Id, year, 5_000, 10_000, "Certificate"));
    Assert.Equal(10_000, EmployeeTaxOpening.Create(Employee().Id, year, 500_000, 10_000, "Certificate from C&W").TaxWithheld);
  }

  [Fact]
  public void A_cheque_or_cash_payment_needs_no_bank_account_a_transfer_does()
  {
    var period = PayrollPeriod.Create(PayrollPeriodId.New(), 2026, 10, null, null);
    var run = PayrollRun.Create(PayrollRunId.New(), period, PayrollRunType.Regular, null, null, Preparer);
    var employee = Employee();
    var slip = PayrollTransaction.Open(PayrollTransactionId.New(), run, employee);

    // the run is not finalized: both are refused, but for that reason, not for the missing account
    var transfer = Assert.Throws<DomainException>(() => PayrollPayment.Create(PayrollPaymentId.New(), run, slip, null, PaymentMethod.BankTransfer));
    Assert.Contains("bank account", transfer.Message);
    var cheque = Assert.Throws<DomainException>(() => PayrollPayment.Create(PayrollPaymentId.New(), run, slip, null, PaymentMethod.Cheque));
    Assert.DoesNotContain("bank account", cheque.Message);
  }

  [Fact]
  public void A_rule_for_a_unit_reaches_the_units_under_it()
  {
    var wing = OrganizationUnitId.New();
    var division = OrganizationUnitId.New();
    var rule = RuleFor(Component("WING_ALW"), Rule(fixedAmount: 1_000) with { ApplicableOrgUnitId = wing });

    Assert.True(rule.Matches(new RuleScope(17, DesignationId.New(), division, EmploymentTypeId.New(), D("2026-10-01"), [division, wing])));
    Assert.False(rule.Matches(new RuleScope(17, DesignationId.New(), division, EmploymentTypeId.New(), D("2026-10-01"), [division])));
  }
}

public class PerRuleTests
{
  private static readonly DateTime Now = new(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);

  private static (PerformanceReview Review, Employee Employee, Employee Officer) Submitted(bool countersigned = true)
  {
    var employee = Employee();
    var period = PerformancePeriod.Create(PerformancePeriodId.New(), "PER 2025-26", D("2025-07-01"), D("2026-06-30"));
    var review = PerformanceReview.Create(PerformanceReviewId.New(), employee, period, Employee());
    var officer = Employee();
    if (countersigned)
      review.SetCountersigningOfficer(officer);
    review.SetContent([new GoalInput("Water scheme", 100, null, null, 85)], [new KpiInput("Files", "100", "90", 70)], [new CompetencyInput("Teamwork", 8, null)]);
    review.Submit(Now);
    return (review, employee, officer);
  }

  [Fact]
  public void The_employee_sees_the_review_only_once_it_is_submitted()
  {
    var period = PerformancePeriod.Create(PerformancePeriodId.New(), "PER", D("2025-07-01"), D("2026-06-30"));
    var draft = PerformanceReview.Create(PerformanceReviewId.New(), Employee(), period, Employee());

    Assert.False(draft.IsVisibleToEmployee);
    Assert.True(Submitted().Review.IsVisibleToEmployee);
  }

  [Fact]
  public void The_form_and_recommendations_are_locked_once_submitted()
  {
    var (review, _, _) = Submitted();

    Assert.Throws<DomainException>(() => review.SetRecommendations(true, false, "Promote"));
    Assert.Throws<DomainException>(() => review.SetContent([], [], []));
  }

  [Fact]
  public void A_review_with_a_countersigning_officer_is_countersigned_before_it_is_acknowledged_or_finalized()
  {
    var (review, _, officer) = Submitted();

    Assert.Throws<DomainException>(() => review.Acknowledge(Now));
    Assert.Throws<DomainException>(() => review.FinalizeReview(null, null, ScoreWeights.GoalsOnly));
    Assert.Throws<DomainException>(() => review.Countersign(Employee().Id, "Agree", null, Now));
    review.Countersign(officer.Id, "I agree, but the grading is one step lower.", "Good", Now);
    review.Acknowledge(Now);
    review.FinalizeReview(null, null, ScoreWeights.GoalsOnly);
    Assert.Equal(("Good", ReviewStatus.Finalized), (review.PerformanceGrade, review.Status));
  }

  [Fact]
  public void The_score_weights_count_kpis_and_competencies_when_the_setting_says_so()
  {
    var (review, _, _) = Submitted(countersigned: false);

    Assert.Equal(85m, review.ComputedScore(ScoreWeights.GoalsOnly));
    // 60% goals (85) + 20% KPIs (70) + 20% competencies (8/10 = 80)
    Assert.Equal(81m, review.ComputedScore(new ScoreWeights(60, 20, 20)));
  }

  [Fact]
  public void A_final_score_different_from_the_form_needs_the_reason()
  {
    var (review, _, _) = Submitted(countersigned: false);

    Assert.Throws<DomainException>(() => review.FinalizeReview(90, null, ScoreWeights.GoalsOnly));
    review.FinalizeReview(90, null, ScoreWeights.GoalsOnly, "Representation accepted");
    Assert.Equal("Representation accepted", review.FinalScoreReason);
  }
}
