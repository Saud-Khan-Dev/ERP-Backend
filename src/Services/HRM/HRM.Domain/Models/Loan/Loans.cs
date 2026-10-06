/// A kind of loan or advance (Motor Car Advance, GP Fund Advance ...). salary_component_id = the pay-slip deduction
/// line its installments are recovered on, so a loan is never counted twice.
public class LoanType : Aggregate<LoanTypeId>
{
  public string Name { get; private set; } = default!;
  public SalaryComponentId? SalaryComponentId { get; private set; }
  public bool IsGpfAdvance { get; private set; }
  public decimal DefaultInterestRate { get; private set; }
  public bool IsActive { get; private set; }

  public static LoanType Create(LoanTypeId id, string name, SalaryComponent? deductionComponent, bool isGpfAdvance, decimal defaultInterestRate)
  {
    var type = new LoanType { Id = id, IsActive = true };
    type.Update(name, deductionComponent, isGpfAdvance, defaultInterestRate);
    return type;
  }

  public void Update(string name, SalaryComponent? deductionComponent, bool isGpfAdvance, decimal defaultInterestRate)
  {
    if (deductionComponent is not null)
    {
      if (deductionComponent.ComponentType != ComponentType.Deduction)
        throw new DomainException($"'{deductionComponent.ComponentName}' is an earning; installments are recovered on a deduction line.");
      if (deductionComponent.IsSystem)
        throw new DomainException($"'{deductionComponent.ComponentName}' is worked out by the payroll engine and cannot carry loan installments.");
    }

    Name = Guard.RequiredText(name, 100, "Loan type name");
    SalaryComponentId = deductionComponent?.Id;
    IsGpfAdvance = isGpfAdvance;
    DefaultInterestRate = Guard.Between(defaultInterestRate, 0, 100, "Interest rate");
  }

  public void SetActive(bool isActive) => IsActive = isActive;

  public void EnsureActive()
  {
    if (!IsActive)
      throw new DomainException($"Loan type '{Name}' is inactive.");
  }
}

/// How much of a payment went to which installment.
public sealed record InstallmentPayment(LoanInstallmentId InstallmentId, int InstallmentNumber, decimal Amount);

/// A loan or advance to an employee with its monthly installment schedule. Payroll recovers due installments when a run
/// is finalized; repayments outside payroll are applied to the oldest open installments. remaining_balance is principal
/// plus interest still to recover.
public class EmployeeLoan : Aggregate<EmployeeLoanId>
{
  private readonly List<LoanInstallment> _installments = new();

  public EmployeeId EmployeeId { get; private set; } = default!;
  public LoanTypeId LoanTypeId { get; private set; } = default!;
  public decimal PrincipalAmount { get; private set; }
  public decimal InterestAmount { get; private set; }
  public int InstallmentsCount { get; private set; }
  public decimal MonthlyInstallment { get; private set; }
  public DateOnly StartDate { get; private set; }
  public DateOnly? EndDate { get; private set; }
  public decimal RemainingBalance { get; private set; }
  /// Lower numbers are recovered first when the pay cannot cover every deduction.
  public int DeductionPriority { get; private set; }
  public LoanStatus Status { get; private set; }
  public Guid? ApprovedBy { get; private set; }

  public IReadOnlyList<LoanInstallment> Installments => _installments.OrderBy(i => i.InstallmentNumber).ToList().AsReadOnly();

  public decimal TotalRepayable => PrincipalAmount + InterestAmount;

  /// Sanctions a loan and lays out its schedule: equal monthly installments due from the start date, the last one
  /// absorbing the rounding. Interest defaults to the loan type's simple annual rate over the term.
  public static EmployeeLoan Sanction(
      EmployeeLoanId id,
      Employee employee,
      LoanType loanType,
      decimal principalAmount,
      decimal? interestAmount,
      int installmentsCount,
      DateOnly startDate,
      int deductionPriority,
      Guid? approvedBy)
  {
    ArgumentNullException.ThrowIfNull(employee);
    ArgumentNullException.ThrowIfNull(loanType);
    employee.EnsureInService();
    loanType.EnsureActive();
    if (loanType.SalaryComponentId is null)
      throw new DomainException($"'{loanType.Name}' has no deduction line to recover installments on; set its salary component first.");

    var principal = Guard.Money(Guard.Positive(principalAmount, "Principal"), "Principal");
    var count = Guard.Between(installmentsCount, 1, 600, "Number of installments");
    var interest = interestAmount.HasValue
      ? Guard.Money(interestAmount.Value, "Interest")
      : decimal.Round(principal * loanType.DefaultInterestRate / 100m * count / 12m, 2, MidpointRounding.AwayFromZero);

    var loan = new EmployeeLoan
    {
      Id = id,
      EmployeeId = employee.Id,
      LoanTypeId = loanType.Id,
      PrincipalAmount = principal,
      InterestAmount = interest,
      InstallmentsCount = count,
      StartDate = startDate,
      DeductionPriority = deductionPriority,
      Status = LoanStatus.Active,
      ApprovedBy = Guard.Actor(approvedBy, "sanction a loan")
    };

    var total = loan.TotalRepayable;
    var monthly = decimal.Round(total / count, 2, MidpointRounding.AwayFromZero);
    if (monthly <= 0)
      throw new DomainException("The installment would be zero; take fewer installments.");

    for (var n = 1; n <= count; n++)
    {
      var amount = n < count ? monthly : total - monthly * (count - 1);
      if (amount <= 0)
        throw new DomainException("The last installment would be zero or less; take fewer installments.");
      loan._installments.Add(LoanInstallment.Create(loan.Id, n, startDate.AddMonths(n - 1), amount));
    }

    loan.MonthlyInstallment = monthly;
    loan.EndDate = startDate.AddMonths(count - 1);
    loan.RemainingBalance = total;
    return loan;
  }

  public bool IsActive => Status == LoanStatus.Active;

  /// Open installments due on or before a date, oldest first.
  public IEnumerable<LoanInstallment> DueBy(DateOnly date) =>
      _installments.Where(i => i.IsOpen && i.DueDate <= date).OrderBy(i => i.InstallmentNumber);

  public LoanInstallment Installment(LoanInstallmentId installmentId) =>
      _installments.FirstOrDefault(i => i.Id == installmentId)
      ?? throw new DomainException("The installment does not belong to this loan.");

  /// Payroll recovered (part of) an installment when its run was finalized.
  public void RecordPayrollRecovery(LoanInstallmentId installmentId, decimal amount)
  {
    EnsureActive();
    Installment(installmentId).Pay(amount);
    Reduce(amount);
  }

  /// A finalized run was reversed: the recovery is undone. Payroll can deduct an installment only once, so the undone
  /// amount is re-issued as a new installment due on the same date (the original is closed as superseded).
  public LoanInstallment? UndoPayrollRecovery(LoanInstallmentId installmentId, decimal amount)
  {
    var installment = Installment(installmentId);
    installment.Unpay(amount);
    RemainingBalance += amount;
    if (Status == LoanStatus.Completed)
      Status = LoanStatus.Active;

    var outstanding = installment.Amount - installment.PaidAmount;
    if (outstanding <= 0)
      return null;

    installment.Supersede();
    var reissued = LoanInstallment.Create(Id, _installments.Max(i => i.InstallmentNumber) + 1, installment.DueDate, outstanding);
    _installments.Add(reissued);
    return reissued;
  }

  /// A repayment outside payroll (cash, bank deposit): applied to the oldest open installments.
  public IReadOnlyList<InstallmentPayment> Repay(decimal amount)
  {
    EnsureActive();
    amount = Guard.Money(Guard.Positive(amount, "Repayment"), "Repayment");
    if (amount > RemainingBalance)
      throw new DomainException($"The repayment ({amount:N2}) is more than the balance still owed ({RemainingBalance:N2}).");

    var applied = new List<InstallmentPayment>();
    var left = amount;
    foreach (var installment in _installments.Where(i => i.IsOpen).OrderBy(i => i.InstallmentNumber))
    {
      if (left <= 0)
        break;

      var part = Math.Min(left, installment.Amount - installment.PaidAmount);
      installment.Pay(part);
      applied.Add(new InstallmentPayment(installment.Id, installment.InstallmentNumber, part));
      left -= part;
    }

    Reduce(amount - left);
    return applied;
  }

  /// Forgives the rest of one installment (sanctioned by the competent authority).
  public decimal WaiveInstallment(LoanInstallmentId installmentId)
  {
    EnsureActive();
    var installment = Installment(installmentId);
    var forgiven = installment.Amount - installment.PaidAmount;
    installment.Waive();
    Reduce(forgiven);
    return forgiven;
  }

  /// Withdraws a loan before anything was recovered.
  public void Cancel()
  {
    EnsureActive();
    if (_installments.Any(i => i.PaidAmount > 0))
      throw new DomainException("Installments have already been recovered; write the loan off instead of cancelling it.");

    Status = LoanStatus.Cancelled;
  }

  /// Gives up on the balance still owed (it stays on record as remaining_balance).
  public void WriteOff()
  {
    EnsureActive();
    Status = LoanStatus.WrittenOff;
  }

  public void ChangePriority(int deductionPriority)
  {
    EnsureActive();
    DeductionPriority = deductionPriority;
  }

  private void Reduce(decimal amount)
  {
    RemainingBalance = Math.Max(0, RemainingBalance - amount);
    if (RemainingBalance == 0)
      Status = LoanStatus.Completed;
  }

  private void EnsureActive()
  {
    if (Status != LoanStatus.Active)
      throw new DomainException($"The loan is {EnumText.Words(Status)}.");
  }
}

public class LoanInstallment : Entity<LoanInstallmentId>
{
  public EmployeeLoanId EmployeeLoanId { get; private set; } = default!;
  public int InstallmentNumber { get; private set; }
  public DateOnly DueDate { get; private set; }
  public decimal Amount { get; private set; }
  public decimal PaidAmount { get; private set; }
  public InstallmentStatus Status { get; private set; }

  public bool IsOpen => Status is InstallmentStatus.Pending or InstallmentStatus.Partial;

  public decimal Outstanding => IsOpen ? Amount - PaidAmount : 0;

  internal static LoanInstallment Create(EmployeeLoanId loanId, int number, DateOnly dueDate, decimal amount) => new()
  {
    Id = LoanInstallmentId.New(),
    EmployeeLoanId = loanId,
    InstallmentNumber = Guard.Positive(number, "Installment number"),
    DueDate = dueDate,
    Amount = Guard.Money(Guard.Positive(amount, "Installment"), "Installment"),
    Status = InstallmentStatus.Pending
  };

  internal void Pay(decimal amount)
  {
    if (!IsOpen)
      throw new DomainException($"Installment {InstallmentNumber} is {EnumText.Words(Status)}.");
    if (amount <= 0 || PaidAmount + amount > Amount)
      throw new DomainException($"Installment {InstallmentNumber} has {Amount - PaidAmount:N2} left to pay.");

    PaidAmount += amount;
    Status = PaidAmount >= Amount ? InstallmentStatus.Paid : InstallmentStatus.Partial;
  }

  internal void Unpay(decimal amount)
  {
    if (amount <= 0 || amount > PaidAmount)
      throw new DomainException($"Installment {InstallmentNumber} has only {PaidAmount:N2} paid.");

    PaidAmount -= amount;
    Status = PaidAmount == 0 ? InstallmentStatus.Pending : InstallmentStatus.Partial;
  }

  internal void Waive()
  {
    if (!IsOpen)
      throw new DomainException($"Installment {InstallmentNumber} is already {EnumText.Words(Status)}.");

    Status = InstallmentStatus.Waived;
  }

  /// Replaced by a re-issued installment: what was paid stays paid; the rest moves to the new installment.
  internal void Supersede()
  {
    if (PaidAmount > 0)
    {
      Amount = PaidAmount;
      Status = InstallmentStatus.Paid;
    }
    else
    {
      Status = InstallmentStatus.Waived;
    }
  }
}

/// An employee's General Provident Fund account (one per employee). The balance is the sum of its ledger.
public class GpFundAccount : Aggregate<GpFundAccountId>
{
  public EmployeeId EmployeeId { get; private set; } = default!;
  public string? AccountNumber { get; private set; }
  public DateOnly OpenedOn { get; private set; }
  public DateOnly? ClosedOn { get; private set; }
  public decimal MonthlySubscription { get; private set; }
  public RecordStatus Status { get; private set; }

  public bool IsOpen => Status == RecordStatus.Active;

  public static GpFundAccount Open(GpFundAccountId id, Employee employee, string? accountNumber, DateOnly openedOn, decimal monthlySubscription)
  {
    ArgumentNullException.ThrowIfNull(employee);
    employee.EnsureInService();

    return new GpFundAccount
    {
      Id = id,
      EmployeeId = employee.Id,
      AccountNumber = Guard.OptionalCode(accountNumber, 50, "GP Fund account number"),
      OpenedOn = openedOn,
      MonthlySubscription = Guard.Money(monthlySubscription, "Monthly subscription"),
      Status = RecordStatus.Active
    };
  }

  public void Update(string? accountNumber, decimal monthlySubscription)
  {
    EnsureOpen();
    AccountNumber = Guard.OptionalCode(accountNumber, 50, "GP Fund account number");
    MonthlySubscription = Guard.Money(monthlySubscription, "Monthly subscription");
  }

  /// The account is settled (final payment made) and closed.
  public void Close(DateOnly closedOn, decimal balance)
  {
    EnsureOpen();
    if (closedOn < OpenedOn)
      throw new DomainException("An account cannot close before it opened.");
    if (balance != 0)
      throw new DomainException($"The account still holds {balance:N2}; record the final payment before closing it.");

    ClosedOn = closedOn;
    Status = RecordStatus.Inactive;
  }

  public void EnsureOpen()
  {
    if (!IsOpen)
      throw new DomainException("The GP Fund account is closed.");
  }
}

/// The GP Fund interest rate notified for a fiscal year. Periods never overlap.
public class GpFundInterestRate : Aggregate<GpFundInterestRateId>
{
  public string FiscalYear { get; private set; } = default!;
  public decimal RatePercent { get; private set; }
  public string? NotificationRef { get; private set; }
  public DateOnly EffectiveFrom { get; private set; }
  public DateOnly? EffectiveTo { get; private set; }

  public DateRange Range => new(EffectiveFrom, EffectiveTo);

  public static GpFundInterestRate Create(GpFundInterestRateId id, string fiscalYear, decimal ratePercent, string? notificationRef, DateOnly effectiveFrom, DateOnly? effectiveTo)
  {
    var rate = new GpFundInterestRate { Id = id };
    rate.Update(fiscalYear, ratePercent, notificationRef, effectiveFrom, effectiveTo);
    return rate;
  }

  public void Update(string fiscalYear, decimal ratePercent, string? notificationRef, DateOnly effectiveFrom, DateOnly? effectiveTo)
  {
    DateRange.EnsureValid(effectiveFrom, effectiveTo);
    FiscalYear = Guard.RequiredText(fiscalYear, 20, "Fiscal year");
    RatePercent = Guard.Between(ratePercent, 0, 100, "Interest rate");
    NotificationRef = Guard.Text(notificationRef, 200, "Notification");
    EffectiveFrom = effectiveFrom;
    EffectiveTo = effectiveTo;
  }
}

/// Append-only GP Fund ledger row. Signed: opening, subscription, interest and advance recovery add (+); advance,
/// withdrawal and final payment take (-); an adjustment goes either way. Never updated or deleted (a trigger refuses it).
public class GpFundTransaction : Aggregate<GpFundTransactionId>
{
  public GpFundAccountId GpFundAccountId { get; private set; } = default!;
  public GpFundTransactionType TransactionType { get; private set; }
  public DateOnly TransactionDate { get; private set; }
  public decimal Amount { get; private set; }
  public EmployeeLoanId? EmployeeLoanId { get; private set; }
  public PayrollTransactionId? PayrollTransactionId { get; private set; }
  public string? Remarks { get; private set; }

  public static GpFundTransaction Opening(GpFundAccount account, DateOnly date, decimal amount, string? remarks) =>
      Create(account, GpFundTransactionType.Opening, date, Guard.Positive(amount, "Opening balance"), null, null, remarks);

  public static GpFundTransaction Subscription(GpFundAccount account, DateOnly date, decimal amount, PayrollTransactionId? payrollTransactionId, string? remarks) =>
      Create(account, GpFundTransactionType.Subscription, date, Guard.Positive(amount, "Subscription"), null, payrollTransactionId, remarks);

  public static GpFundTransaction Interest(GpFundAccount account, DateOnly date, decimal amount, string? remarks) =>
      Create(account, GpFundTransactionType.Interest, date, Guard.Positive(amount, "Interest"), null, null, remarks);

  /// Money lent out of the fund to the employee (a GP Fund advance).
  public static GpFundTransaction Advance(GpFundAccount account, DateOnly date, decimal amount, EmployeeLoanId loanId, decimal balance) =>
      amount > balance
        ? throw new DomainException($"The advance ({amount:N2}) is more than the GP Fund balance ({balance:N2}).")
        : Create(account, GpFundTransactionType.Advance, date, -Guard.Positive(amount, "Advance"), loanId, null, "GP Fund advance");

  public static GpFundTransaction AdvanceRecovery(GpFundAccount account, DateOnly date, decimal amount, EmployeeLoanId loanId, PayrollTransactionId? payrollTransactionId, string remarks) =>
      Create(account, GpFundTransactionType.AdvanceRecovery, date, Guard.Positive(amount, "Recovery"), loanId, payrollTransactionId, remarks);

  public static GpFundTransaction Withdrawal(GpFundAccount account, DateOnly date, decimal amount, decimal balance, string? remarks) =>
      amount > balance
        ? throw new DomainException($"The withdrawal ({amount:N2}) is more than the GP Fund balance ({balance:N2}).")
        : Create(account, GpFundTransactionType.Withdrawal, date, -Guard.Positive(amount, "Withdrawal"), null, null, remarks);

  public static GpFundTransaction FinalPayment(GpFundAccount account, DateOnly date, decimal balance, string? remarks) =>
      Create(account, GpFundTransactionType.FinalPayment, date, -Guard.Positive(balance, "Balance to pay"), null, null, remarks);

  /// Signed correction; also used to undo a recovery of a reversed payroll run.
  public static GpFundTransaction Adjustment(GpFundAccount account, DateOnly date, decimal amount, EmployeeLoanId? loanId, PayrollTransactionId? payrollTransactionId, string remarks)
  {
    if (amount == 0)
      throw new DomainException("An adjustment of zero changes nothing.");
    return Create(account, GpFundTransactionType.Adjustment, date, amount, loanId, payrollTransactionId, Guard.RequiredText(remarks, 2000, "Reason"));
  }

  private static GpFundTransaction Create(
      GpFundAccount account,
      GpFundTransactionType type,
      DateOnly date,
      decimal amount,
      EmployeeLoanId? loanId,
      PayrollTransactionId? payrollTransactionId,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(account);

    if (date < account.OpenedOn)
      throw new DomainException($"The GP Fund account opened on {account.OpenedOn:yyyy-MM-dd}; nothing can be dated before that.");

    return new GpFundTransaction
    {
      Id = GpFundTransactionId.New(),
      GpFundAccountId = account.Id,
      TransactionType = type,
      TransactionDate = date,
      Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
      EmployeeLoanId = loanId,
      PayrollTransactionId = payrollTransactionId,
      Remarks = Guard.Text(remarks, 2000, "Remarks")
    };
  }
}
