/// Builders for the objects most tests need: employees, salary components and rules, a tax year, loans.
internal static class Fixture
{
  public static readonly Guid Officer = Guid.Parse("11111111-1111-1111-1111-111111111111");

  public static DateOnly D(string iso) => DateOnly.Parse(iso);

  private static int _employees;

  public static Employee Employee(DateOnly? dateOfBirth = null, EmploymentType type = EmploymentType.Regular)
  {
    var n = ++_employees;
    return global::Employee.Create(EmployeeId.New(), $"EMP-{n:000}",
      new PersonalDetails("Test", null, $"Employee {n}", $"35202{n:0000000}1", dateOfBirth ?? D("1985-05-10"), Gender.Male, "Pakistani", null, null),
      type, type == EmploymentType.Regular ? EmploymentMethod.ScheduledSeat : null, null, D("2026-10-06"));
  }

  public static SalaryComponent Component(string code, ComponentType type = ComponentType.Earning, bool taxable = true) =>
      SalaryComponent.Create(SalaryComponentId.New(), code, code, type, type == ComponentType.Earning && taxable);

  public static SalaryRuleDefinition Rule(
      CalculationMethod method = CalculationMethod.Fixed,
      decimal? fixedAmount = null,
      decimal? percentage = null,
      string? calculationBase = null,
      string? formula = null,
      int? minBps = null,
      int? maxBps = null,
      int priority = 10,
      string from = "2025-07-01",
      string? to = null,
      decimal? minAmount = null,
      decimal? maxAmount = null,
      string version = "v1",
      DesignationId? designation = null) =>
      new(version, method, fixedAmount, percentage, calculationBase, formula, minBps, maxBps, designation, null, null, minAmount, maxAmount,
        priority, null, D(from), to is null ? null : D(to));

  public static SalaryComponentRule RuleFor(SalaryComponent component, SalaryRuleDefinition definition) =>
      SalaryComponentRule.Create(SalaryComponentRuleId.New(), component, definition, Officer);

  /// FY 2025-26 salaried slabs: 0-600k nil, 1% to 1.2M, 6,000 + 11% to 2.2M, 116,000 + 23% to 3.2M, 346,000 + 30% to 4.1M, 616,000 + 35% above.
  public static TaxYear TaxYear(string label = "2026-27", string from = "2026-07-01", string to = "2027-06-30") =>
      global::TaxYear.Create(TaxYearId.New(), label, D(from), D(to),
      [
        new TaxSlabInput(1, 0, 600_000, 0, 0),
        new TaxSlabInput(2, 600_000, 1_200_000, 0, 1),
        new TaxSlabInput(3, 1_200_000, 2_200_000, 6_000, 11),
        new TaxSlabInput(4, 2_200_000, 3_200_000, 116_000, 23),
        new TaxSlabInput(5, 3_200_000, 4_100_000, 346_000, 30),
        new TaxSlabInput(6, 4_100_000, null, 616_000, 35)
      ]);

  public static LoanType LoanType(SalaryComponent deduction, bool gpfAdvance = false, decimal rate = 0) =>
      global::LoanType.Create(LoanTypeId.New(), gpfAdvance ? "GP Fund Advance" : "Motor Car Advance", deduction, gpfAdvance, rate);

  public static EmployeeLoan Loan(LoanType type, decimal principal, int installments, string start = "2026-10-01", Employee? employee = null) =>
      EmployeeLoan.Sanction(EmployeeLoanId.New(), employee ?? Employee(), type, principal, null, installments, D(start), 100, Officer);
}
