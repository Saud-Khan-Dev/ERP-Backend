using static Fixture;

public class TaxTests
{
  [Theory]
  [InlineData(0, 0)]
  [InlineData(600_000, 0)]
  [InlineData(1_200_000, 6_000)]
  [InlineData(1_800_000, 72_000)]
  [InlineData(5_000_000, 931_000)]
  public void Annual_tax_is_the_slab_fixed_amount_plus_its_rate_above_the_slab_minimum(decimal income, decimal tax) =>
      Assert.Equal(tax, TaxYear().AnnualTax(income));

  [Fact]
  public void Slabs_may_not_overlap_and_only_the_highest_may_be_open_ended()
  {
    Assert.Throws<DomainException>(() => global::TaxYear.Create(TaxYearId.New(), "X", D("2030-07-01"), D("2031-06-30"),
      [new TaxSlabInput(1, 0, 100, 0, 0), new TaxSlabInput(2, 50, null, 0, 5)]));
    Assert.Throws<DomainException>(() => global::TaxYear.Create(TaxYearId.New(), "X", D("2030-07-01"), D("2031-06-30"),
      [new TaxSlabInput(1, 0, null, 0, 0), new TaxSlabInput(2, 100, 200, 0, 5)]));
  }

  [Fact]
  public void Months_remaining_counts_this_month_to_june()
  {
    var year = TaxYear();

    Assert.Equal(12, year.MonthsRemainingFrom(2026, 7));
    Assert.Equal(9, year.MonthsRemainingFrom(2026, 10));
    Assert.Equal(1, year.MonthsRemainingFrom(2027, 6));
    Assert.Equal(0, year.MonthsRemainingFrom(2027, 7));
  }
}

public class LoanTests
{
  private static readonly SalaryComponent Deduction = Component("MCA", ComponentType.Deduction);

  [Fact]
  public void The_last_installment_absorbs_the_rounding()
  {
    var loan = Loan(LoanType(Deduction), 100_000, 3);

    Assert.Equal([33_333.33m, 33_333.33m, 33_333.34m], loan.Installments.Select(i => i.Amount));
    Assert.Equal(100_000, loan.RemainingBalance);
    Assert.Equal(D("2026-12-01"), loan.EndDate);
  }

  [Fact]
  public void Interest_defaults_to_the_types_simple_annual_rate_over_the_term()
  {
    var loan = Loan(LoanType(Deduction, rate: 10), 120_000, 24);

    Assert.Equal(24_000, loan.InterestAmount);
    Assert.Equal(144_000, loan.RemainingBalance);
  }

  [Fact]
  public void A_loan_type_needs_a_deduction_line_before_loans_can_be_sanctioned()
  {
    var type = global::LoanType.Create(LoanTypeId.New(), "House Building Advance", null, false, 0);

    Assert.Throws<DomainException>(() => Loan(type, 1_000, 1));
    Assert.Throws<DomainException>(() => global::LoanType.Create(LoanTypeId.New(), "X", Component("MEDICAL"), false, 0));
  }

  [Fact]
  public void A_repayment_is_applied_to_the_oldest_installments_first()
  {
    var loan = Loan(LoanType(Deduction), 30_000, 3);

    var applied = loan.Repay(15_000);

    Assert.Equal([10_000m, 5_000m], applied.Select(a => a.Amount));
    Assert.Equal([InstallmentStatus.Paid, InstallmentStatus.Partial, InstallmentStatus.Pending], loan.Installments.Select(i => i.Status));
    Assert.Equal(15_000, loan.RemainingBalance);
    Assert.Throws<DomainException>(() => loan.Repay(15_000.01m));
  }

  [Fact]
  public void Recovering_every_installment_completes_the_loan()
  {
    var loan = Loan(LoanType(Deduction), 20_000, 2);
    foreach (var installment in loan.Installments)
      loan.RecordPayrollRecovery(installment.Id, installment.Amount);

    Assert.Equal(LoanStatus.Completed, loan.Status);
    Assert.Equal(0, loan.RemainingBalance);
  }

  [Fact]
  public void Undoing_a_payroll_recovery_reissues_the_installment_for_the_same_date()
  {
    var loan = Loan(LoanType(Deduction), 20_000, 2);
    var first = loan.Installments[0];
    loan.RecordPayrollRecovery(first.Id, first.Amount);

    var reissued = loan.UndoPayrollRecovery(first.Id, first.Amount);

    Assert.NotNull(reissued);
    Assert.Equal(first.DueDate, reissued.DueDate);
    Assert.Equal(10_000, reissued.Amount);
    Assert.False(first.IsOpen);
    Assert.Equal(20_000, loan.RemainingBalance);
    Assert.Contains(reissued, loan.DueBy(D("2026-10-31")));
  }

  [Fact]
  public void A_loan_with_recoveries_is_written_off_not_cancelled()
  {
    var loan = Loan(LoanType(Deduction), 20_000, 2);
    loan.Repay(1_000);

    Assert.Throws<DomainException>(() => loan.Cancel());
    loan.WriteOff();
    Assert.Equal(LoanStatus.WrittenOff, loan.Status);
    Assert.Throws<DomainException>(() => loan.Repay(1_000));
  }

  [Fact]
  public void A_loan_is_only_sanctioned_to_an_employee_in_service()
  {
    var employee = Employee();
    employee.ChangeEmploymentStatus(EmploymentStatus.Retired);

    Assert.Throws<DomainException>(() => Loan(LoanType(Deduction), 1_000, 1, employee: employee));
  }
}

public class GpFundTests
{
  private static GpFundAccount Account(string opened = "2025-07-01") =>
      GpFundAccount.Open(GpFundAccountId.New(), Employee(), "GPF-1", D(opened), 3_000);

  [Fact]
  public void Interest_is_the_monthly_rate_on_each_month_end_balance()
  {
    // 120,000 all year at 12%: 12 x 1,200; a 12,000 subscription in January earns 6 months more
    var ledger = new List<(DateOnly, decimal)> { (D("2025-07-01"), 120_000), (D("2026-01-15"), 12_000) };

    Assert.Equal(14_400 + 720, GpFundInterest.ForYear(ledger, D("2025-07-01"), D("2026-06-30"), 12));
  }

  [Fact]
  public void Months_with_no_balance_earn_nothing()
  {
    var ledger = new List<(DateOnly, decimal)> { (D("2026-04-10"), 10_000) };

    Assert.Equal(300, GpFundInterest.ForYear(ledger, D("2025-07-01"), D("2026-06-30"), 12));
  }

  [Fact]
  public void An_advance_or_withdrawal_cannot_exceed_the_balance()
  {
    var account = Account();

    Assert.Throws<DomainException>(() => GpFundTransaction.Advance(account, D("2026-10-01"), 60_000, EmployeeLoanId.New(), 50_000));
    Assert.Throws<DomainException>(() => GpFundTransaction.Withdrawal(account, D("2026-10-01"), 60_000, 50_000, null));
    Assert.Equal(-50_000, GpFundTransaction.Advance(account, D("2026-10-01"), 50_000, EmployeeLoanId.New(), 50_000).Amount);
  }

  [Fact]
  public void Nothing_is_dated_before_the_account_opened() =>
      Assert.Throws<DomainException>(() => GpFundTransaction.Opening(Account("2026-01-01"), D("2025-12-31"), 1_000, null));

  [Fact]
  public void An_account_closes_only_when_settled()
  {
    var account = Account();

    Assert.Throws<DomainException>(() => account.Close(D("2026-10-01"), 1_000));
    account.Close(D("2026-10-01"), 0);
    Assert.False(account.IsOpen);
  }
}
