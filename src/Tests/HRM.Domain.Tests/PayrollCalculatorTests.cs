using static Fixture;

public class PayrollCalculatorTests
{
  // June 2026: 30 days, so prorations are easy to read
  private static readonly DateRange June = new(D("2026-06-01"), D("2026-06-30"));

  private static readonly SalaryComponent Basic = Component(SystemComponents.BasicPay);
  private static readonly SalaryComponent Tax = Component(SystemComponents.IncomeTax, ComponentType.Deduction);
  private static readonly SalaryComponent Gpf = Component(SystemComponents.GpFund, ComponentType.Deduction);
  private static readonly SalaryComponent Allowance = Component("GDA_AUTH");
  private static readonly SalaryComponent Medical = Component("MEDICAL", taxable: false);
  private static readonly SalaryComponent Benevolent = Component("BF", ComponentType.Deduction);
  private static readonly SalaryComponent CarAdvance = Component("MCA", ComponentType.Deduction);

  private static readonly PostId PostA = PostId.New();
  private static readonly PostId PostB = PostId.New();
  private static readonly DesignationId Designation = DesignationId.New();
  private static readonly OrganizationUnitId Unit = OrganizationUnitId.New();

  private static PayrollCatalog Catalog(params SalaryComponentRule[] rules) =>
      new([Basic, Tax, Gpf, Allowance, Medical, Benevolent, CarAdvance], rules, Basic, Tax, Gpf, new HashSet<SalaryComponentId> { CarAdvance.Id });

  private static PostVersionSlice Version(PostId post, int bps, string from = "2020-01-01", string? to = null) =>
      new(post, PayScaleGradeId.New(), bps, Designation, Unit, D(from), to is null ? null : D(to));

  private static PaySlice Pay(decimal basic, string from = "2020-01-01", string? to = null) =>
      new(PayScaleStageId.New(), 3, basic, basic - 10_000, basic + 30_000, D(from), to is null ? null : D(to));

  private static PayrollEmployeeInput Input(
      IReadOnlyList<AssignmentSlice>? assignments = null,
      IReadOnlyList<PostVersionSlice>? versions = null,
      IReadOnlyList<PaySlice>? pay = null,
      IReadOnlyList<DateRange>? unpaid = null,
      IReadOnlyList<EmployeeSalaryComponent>? overrides = null,
      decimal gpf = 0,
      IReadOnlyList<DueInstallment>? installments = null,
      decimal taxableAdjustments = 0,
      decimal netAdjustments = 0,
      TaxInput? tax = null) =>
      new(EmployeeId.New(), EmploymentType.Regular,
        assignments ?? [new AssignmentSlice(PostA, D("2020-01-01"), null)],
        versions ?? [Version(PostA, 17)],
        pay ?? [Pay(60_000)],
        unpaid ?? [], overrides ?? [], gpf, installments ?? [], taxableAdjustments, netAdjustments, tax);

  private static decimal Line(PayCalculation result, SalaryComponent component) =>
      result.Lines.Where(l => l.ComponentId == component.Id).Sum(l => l.Amount);

  private static decimal Net(PayCalculation result) =>
      result.Lines.Where(l => l.ComponentType == ComponentType.Earning).Sum(l => l.Amount)
      - result.Lines.Where(l => l.ComponentType == ComponentType.Deduction).Sum(l => l.Amount);

  [Fact]
  public void A_full_month_pays_basic_and_the_rule_allowances_less_deductions()
  {
    var catalog = Catalog(RuleFor(Allowance, Rule(CalculationMethod.Percentage, percentage: 20, calculationBase: CalculationBases.BasicPay)),
      RuleFor(Benevolent, Rule(fixedAmount: 500)));

    var result = PayrollCalculator.Calculate(June, true, Input(gpf: 3_000), catalog);

    Assert.Equal(30, result.DaysPayable);
    Assert.Single(result.Segments);
    Assert.Equal(60_000, Line(result, Basic));
    Assert.Equal(12_000, Line(result, Allowance));
    Assert.Equal(500, Line(result, Benevolent));
    Assert.Equal(3_000, Line(result, Gpf));
    Assert.Equal(72_000 - 3_500, Net(result));
  }

  [Fact]
  public void A_mid_month_promotion_splits_the_month_into_two_prorated_segments()
  {
    var input = Input(
      assignments: [new AssignmentSlice(PostA, D("2020-01-01"), D("2026-06-15")), new AssignmentSlice(PostB, D("2026-06-16"), null)],
      versions: [Version(PostA, 17), Version(PostB, 18)],
      pay: [Pay(50_000, to: "2026-06-15"), Pay(60_000, from: "2026-06-16")]);

    var result = PayrollCalculator.Calculate(June, true, input, Catalog());

    Assert.Equal(2, result.Segments.Count);
    Assert.Equal([PostA, PostB], result.Segments.Select(s => s.PostId));
    Assert.Equal(25_000 + 30_000, Line(result, Basic));
  }

  [Fact]
  public void Leave_without_pay_days_are_not_paid()
  {
    var result = PayrollCalculator.Calculate(June, true, Input(pay: [Pay(30_000)], unpaid: [new DateRange(D("2026-06-10"), D("2026-06-14"))]), Catalog());

    Assert.Equal(25, result.DaysPayable);
    Assert.Equal(25_000, Line(result, Basic));
  }

  [Fact]
  public void Days_without_a_regular_post_are_not_paid()
  {
    var result = PayrollCalculator.Calculate(June, true, Input(assignments: [new AssignmentSlice(PostA, D("2026-06-11"), null)], pay: [Pay(30_000)]), Catalog());

    Assert.Equal(20, result.DaysPayable);
    Assert.Equal(20_000, Line(result, Basic));
  }

  [Fact]
  public void A_post_held_without_a_pay_record_stops_the_calculation()
  {
    var input = Input(pay: [Pay(30_000, to: "2026-06-10")]);

    Assert.Throws<DomainException>(() => PayrollCalculator.Calculate(June, true, input, Catalog()));
  }

  [Fact]
  public void Whole_month_deductions_are_not_prorated()
  {
    var catalog = Catalog(RuleFor(Benevolent, Rule(fixedAmount: 600)));

    var result = PayrollCalculator.Calculate(June, true, Input(unpaid: [new DateRange(D("2026-06-01"), D("2026-06-15"))], gpf: 2_000), catalog);

    Assert.Equal(600, Line(result, Benevolent));
    Assert.Equal(2_000, Line(result, Gpf));
  }

  [Fact]
  public void Loan_installments_due_are_deducted_and_linked_to_their_installment()
  {
    var installment = new DueInstallment(EmployeeLoanId.New(), LoanInstallmentId.New(), CarAdvance.Id, 100, 1, D("2026-06-01"), 8_000);

    var result = PayrollCalculator.Calculate(June, true, Input(installments: [installment]), Catalog());

    var line = Assert.Single(result.Lines, l => l.Source == ComponentSource.Loan);
    Assert.Equal(8_000, line.Amount);
    Assert.Equal(installment.InstallmentId, line.Loan!.InstallmentId);
  }

  [Fact]
  public void Net_pay_never_goes_below_zero_the_least_important_loan_is_left_for_later()
  {
    var important = new DueInstallment(EmployeeLoanId.New(), LoanInstallmentId.New(), CarAdvance.Id, 1, 1, D("2026-06-01"), 20_000);
    var minor = new DueInstallment(EmployeeLoanId.New(), LoanInstallmentId.New(), CarAdvance.Id, 9, 1, D("2026-06-01"), 20_000);

    var result = PayrollCalculator.Calculate(June, true, Input(pay: [Pay(30_000)], installments: [important, minor]), Catalog());

    Assert.True(Net(result) >= 0);
    var kept = Assert.Single(result.Lines, l => l.Source == ComponentSource.Loan);
    Assert.Equal(important.InstallmentId, kept.Loan!.InstallmentId);
    Assert.NotEmpty(result.Notes);
  }

  [Fact]
  public void The_lowest_priority_number_wins_then_the_most_specific_rule()
  {
    var everyone = RuleFor(Allowance, Rule(fixedAmount: 1_000, priority: 10, version: "all"));
    var seniors = RuleFor(Allowance, Rule(fixedAmount: 5_000, priority: 10, minBps: 17, version: "17+"));
    var special = RuleFor(Allowance, Rule(fixedAmount: 9_000, priority: 1, version: "special"));

    Assert.Equal(5_000, Line(PayrollCalculator.Calculate(June, true, Input(), Catalog(everyone, seniors)), Allowance));
    Assert.Equal(1_000, Line(PayrollCalculator.Calculate(June, true, Input(versions: [Version(PostA, 14)]), Catalog(everyone, seniors)), Allowance));
    Assert.Equal(9_000, Line(PayrollCalculator.Calculate(June, true, Input(), Catalog(everyone, seniors, special)), Allowance));
  }

  [Fact]
  public void A_rule_revised_mid_month_pays_each_part_at_its_own_rate()
  {
    var old = RuleFor(Allowance, Rule(fixedAmount: 3_000, to: "2026-06-15", version: "v1"));
    var revised = RuleFor(Allowance, Rule(fixedAmount: 6_000, from: "2026-06-16", version: "v2"));

    var result = PayrollCalculator.Calculate(June, true, Input(), Catalog(old, revised));

    Assert.Equal(1_500 + 3_000, Line(result, Allowance));
  }

  [Fact]
  public void An_employee_override_replaces_the_rule_amount()
  {
    var employee = Employee();
    var rule = RuleFor(Medical, Rule(fixedAmount: 1_500));
    var @override = EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, Medical, null, 5_000, null, D("2026-01-01"), null, null, []);

    var result = PayrollCalculator.Calculate(June, true, Input(overrides: [@override]), Catalog(rule));

    var line = Assert.Single(result.Lines, l => l.ComponentId == Medical.Id);
    Assert.Equal(5_000, line.Amount);
    Assert.Equal(ComponentSource.Override, line.Source);
  }

  [Fact]
  public void Untaxed_allowances_stay_out_of_taxable_pay()
  {
    var catalog = Catalog(RuleFor(Medical, Rule(fixedAmount: 5_000)));
    var tax = new TaxInput(TaxYear("2025-26", "2025-07-01", "2026-06-30"), 0, 0, 0, 1, 0);

    var result = PayrollCalculator.Calculate(June, true, Input(pay: [Pay(100_000)], tax: tax), catalog);

    Assert.Equal(100_000, result.TaxableIncome);
  }

  [Fact]
  public void Monthly_tax_spreads_the_projected_annual_tax_over_the_months_left()
  {
    var year = TaxYear();
    // October: 9 months left; 81,260 a month, Zakat 25,000 -> 706,340 a year -> 1,063.40 tax -> 118.16 a month
    var tax = new TaxInput(year, 25_000, 0, 0, 9, 0);

    Assert.Equal(118.16m, PayrollCalculator.MonthlyTax(tax, 81_260, 81_260));
  }

  [Fact]
  public void Tax_already_withheld_is_taken_off_what_is_still_due()
  {
    var year = TaxYear();
    // 150,000 a month: 1.8M a year -> 72,000; 3 months paid with 18,000 withheld, 9 left -> (72,000 - 18,000) / 9
    var tax = new TaxInput(year, 0, 450_000, 18_000, 9, 0);

    Assert.Equal(6_000, PayrollCalculator.MonthlyTax(tax, 150_000, 150_000));
  }

  [Fact]
  public void An_arrears_run_withholds_only_the_extra_tax_the_arrears_cause()
  {
    var year = TaxYear();
    // a normal month of 150,000 for the 12 months -> 1.8M; 120,000 arrears push it to 1.92M: 85,200 - 72,000
    var tax = new TaxInput(year, 0, 0, 0, 12, 150_000);

    var result = PayrollCalculator.Calculate(June, false, Input(taxableAdjustments: 120_000, netAdjustments: 120_000, tax: tax), Catalog());

    Assert.Equal(13_200, result.TaxWithheld);
    Assert.Empty(result.Segments);
  }
}
