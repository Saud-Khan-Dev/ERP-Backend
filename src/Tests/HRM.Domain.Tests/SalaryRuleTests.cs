using static Fixture;

public class SalaryRuleTests
{
  private static readonly PayFigures Figures = new(BasicPay: 62_000, MinBasicPay: 50_000, MaxBasicPay: 90_000, GrossPay: 80_000, Bps: 18, Stage: 4, DaysPayable: 31, DaysInPeriod: 31);

  [Theory]
  [InlineData("basic_pay * 0.15", 9_300)]
  [InlineData("max(1500, min_basic_pay * 0.02)", 1_500)]
  [InlineData("if(bps >= 17, 5000, 3000)", 5_000)]
  [InlineData("round(basic_pay / 3, 2)", 20_666.67)]
  [InlineData("(basic_pay - min_basic_pay) * 10%", 1_200)]
  public void Formulas_are_worked_out_on_the_pay_figures(string formula, double expected)
  {
    var parsed = PayFormula.Parse(formula.Replace("10%", "0.10"));

    Assert.Equal((decimal)expected, parsed.Evaluate(Figures.Variables));
  }

  [Theory]
  [InlineData("basic_pay * ; drop table")]
  [InlineData("salary * 2")]
  [InlineData("system(1)")]
  [InlineData("basic_pay * (2")]
  [InlineData("")]
  public void Anything_but_the_small_formula_language_is_refused(string formula) =>
      Assert.Throws<DomainException>(() => PayFormula.Parse(formula));

  [Fact]
  public void Dividing_by_zero_is_a_clear_error_not_a_crash() =>
      Assert.Throws<DomainException>(() => PayFormula.Parse("basic_pay / (bps - 18)").Evaluate(Figures.Variables));

  [Fact]
  public void The_first_band_at_or_above_the_base_decides_a_tiered_amount()
  {
    var tiers = PayTiers.Parse("""[{"upTo": 50000, "amount": 1500}, {"upTo": 100000, "rate": 3}, {"upTo": null, "amount": 4000}]""");

    Assert.Equal(1_500, tiers.Evaluate(50_000));
    Assert.Equal(1_860, tiers.Evaluate(62_000));
    Assert.Equal(4_000, tiers.Evaluate(150_000));
  }

  [Theory]
  [InlineData("""[{"upTo": null, "amount": 1}, {"upTo": 5, "amount": 2}]""")]
  [InlineData("""[{"upTo": 100, "amount": 1}, {"upTo": 50, "amount": 2}]""")]
  [InlineData("""[{"upTo": 100, "amount": 1, "rate": 2}]""")]
  [InlineData("not json")]
  public void Badly_formed_bands_are_refused(string json) => Assert.Throws<DomainException>(() => PayTiers.Parse(json));

  [Fact]
  public void A_rule_is_clamped_to_its_minimum_and_maximum()
  {
    var rule = RuleFor(Component("CONV"), Rule(CalculationMethod.Percentage, percentage: 10, calculationBase: CalculationBases.BasicPay, minAmount: 2_000, maxAmount: 5_000));

    Assert.Equal(5_000, rule.Calculate(Figures).Amount);
    Assert.Equal(2_000, rule.Calculate(Figures with { BasicPay = 10_000 }).Amount);
  }

  [Fact]
  public void Basic_pay_income_tax_and_gp_fund_cannot_have_rules()
  {
    foreach (var code in new[] { SystemComponents.BasicPay, SystemComponents.IncomeTax, SystemComponents.GpFund })
      Assert.Throws<DomainException>(() => RuleFor(Component(code, code == SystemComponents.BasicPay ? ComponentType.Earning : ComponentType.Deduction), Rule(fixedAmount: 1)));
  }

  [Fact]
  public void An_earning_cannot_depend_on_gross_pay()
  {
    var earning = Component("CONV");

    Assert.Throws<DomainException>(() => RuleFor(earning, Rule(CalculationMethod.Percentage, percentage: 5, calculationBase: CalculationBases.GrossPay)));
    Assert.Throws<DomainException>(() => RuleFor(earning, Rule(CalculationMethod.Formula, formula: "gross_pay * 0.01")));
    RuleFor(Component("WELFARE", ComponentType.Deduction), Rule(CalculationMethod.Formula, formula: "gross_pay * 0.01"));
  }

  [Fact]
  public void A_deduction_is_never_taxable_income() =>
      Assert.Throws<DomainException>(() => SalaryComponent.Create(SalaryComponentId.New(), "X", "X", ComponentType.Deduction, true));

  [Fact]
  public void Overrides_of_one_component_may_not_overlap()
  {
    var employee = Employee();
    var medical = Component("MEDICAL");
    var first = EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, medical, null, 5_000, null, D("2026-01-01"), D("2026-06-30"), null, []);

    Assert.Throws<DomainException>(() =>
      EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, medical, null, 6_000, null, D("2026-06-01"), null, null, [first]));
    EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, medical, null, 6_000, null, D("2026-07-01"), null, null, [first]);
  }

  [Fact]
  public void A_percentage_override_uses_the_rules_base_or_basic_pay()
  {
    var employee = Employee();
    var allowance = Component("HOUSING");
    var rule = RuleFor(allowance, Rule(CalculationMethod.Percentage, percentage: 45, calculationBase: CalculationBases.MinBasicPay));
    var @override = EmployeeSalaryComponent.Create(SalaryOverrideId.New(), employee.Id, allowance, null, null, 50, D("2026-01-01"), null, null, []);

    Assert.Equal(25_000, @override.Calculate(Figures, rule).Amount);
    Assert.Equal(31_000, @override.Calculate(Figures, null).Amount);
  }
}
