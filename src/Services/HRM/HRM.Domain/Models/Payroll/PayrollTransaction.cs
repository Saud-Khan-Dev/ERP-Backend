/// One stretch of the period on one post / grade / stage (a mid-month transfer or promotion gives two).
public sealed record SegmentResult(
  int Index,
  PostId PostId,
  PayScaleGradeId GradeId,
  PayScaleStageId? StageId,
  decimal BasicPay,
  DateOnly From,
  DateOnly To,
  decimal Days,
  decimal PayableDays);

public sealed record LoanDeductionResult(EmployeeLoanId LoanId, LoanInstallmentId InstallmentId, decimal Amount);

/// One pay-slip line as the engine worked it out. SegmentIndex links an earning to its segment; deductions belong to
/// the whole month.
public sealed record LineResult(
  int? SegmentIndex,
  SalaryComponentId ComponentId,
  SalaryComponentRuleId? RuleId,
  ComponentSource Source,
  ComponentType ComponentType,
  string? CalculationBase,
  decimal? BaseAmount,
  decimal? Rate,
  string? FormulaReference,
  decimal Amount,
  string? NotificationRef,
  DateOnly? EffectiveDate,
  LoanDeductionResult? Loan = null);

/// The engine's result for one employee in one run.
public sealed record PayCalculation(
  decimal DaysPayable,
  IReadOnlyList<SegmentResult> Segments,
  IReadOnlyList<LineResult> Lines,
  decimal TaxableIncome,
  decimal TaxWithheld,
  IReadOnlyList<string> Notes);

/// The pay-slip header of one employee in one run: gross, deductions and net, with its segments, lines, loan
/// deductions and adjustments. Net = gross - deductions; adjustments are signed (positive raises gross, negative is a
/// deduction). Once its run is finalized only the hold / release status may change.
public class PayrollTransaction : Aggregate<PayrollTransactionId>
{
  private readonly List<PayrollTransactionSegment> _segments = new();
  private readonly List<PayrollComponentDetail> _lines = new();
  private readonly List<PayrollLoanDeduction> _loanDeductions = new();
  private readonly List<PayrollAdjustment> _adjustments = new();

  public PayrollRunId PayrollRunId { get; private set; } = default!;
  public EmployeeId EmployeeId { get; private set; } = default!;
  public decimal? DaysPayable { get; private set; }
  public decimal GrossPay { get; private set; }
  public decimal TotalDeductions { get; private set; }
  public decimal NetPayable { get; private set; }
  public PayrollTransactionStatus Status { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<PayrollTransactionSegment> Segments => _segments.AsReadOnly();
  public IReadOnlyList<PayrollComponentDetail> Lines => _lines.AsReadOnly();
  public IReadOnlyList<PayrollLoanDeduction> LoanDeductions => _loanDeductions.AsReadOnly();
  public IReadOnlyList<PayrollAdjustment> Adjustments => _adjustments.AsReadOnly();

  /// Positive adjustments are pay (arrears, bonus ...) and taxable.
  public decimal TaxableAdjustments => _adjustments.Where(a => a.Amount > 0).Sum(a => a.Amount);

  public bool IsHeld => Status == PayrollTransactionStatus.Held;

  public static PayrollTransaction Open(PayrollTransactionId id, PayrollRun run, Employee employee)
  {
    ArgumentNullException.ThrowIfNull(run);
    ArgumentNullException.ThrowIfNull(employee);
    run.EnsureEditable();

    return new PayrollTransaction
    {
      Id = id,
      PayrollRunId = run.Id,
      EmployeeId = employee.Id,
      Status = PayrollTransactionStatus.Calculated
    };
  }

  /// Replaces the calculated part (segments, lines, loan deductions); adjustments entered by hand are kept.
  public void ApplyCalculation(PayCalculation calculation, IReadOnlyCollection<string>? extraNotes = null)
  {
    ArgumentNullException.ThrowIfNull(calculation);

    _lines.Clear();
    _loanDeductions.Clear();
    _segments.Clear();

    var segments = new Dictionary<int, PayrollTransactionSegment>();
    foreach (var segment in calculation.Segments)
    {
      var row = PayrollTransactionSegment.Create(Id, segment);
      segments[segment.Index] = row;
      _segments.Add(row);
    }

    foreach (var line in calculation.Lines)
    {
      PayrollLoanDeduction? deduction = null;
      if (line.Loan is { } loan)
      {
        deduction = PayrollLoanDeduction.Create(Id, loan);
        _loanDeductions.Add(deduction);
      }

      var segment = line.SegmentIndex is { } index ? segments[index] : null;
      _lines.Add(PayrollComponentDetail.Create(Id, segment?.Id, deduction?.Id, line));
    }

    DaysPayable = calculation.DaysPayable;
    var notes = calculation.Notes.Concat(extraNotes ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
    Remarks = notes.Count == 0 ? null : Guard.Text(string.Join(" ", notes), 4000, "Remarks");
    RecomputeTotals();
  }

  public PayrollAdjustment AddAdjustment(AdjustmentType type, decimal amount, string? reason)
  {
    var adjustment = PayrollAdjustment.Create(Id, type, amount, reason);
    _adjustments.Add(adjustment);
    RecomputeTotals();
    return adjustment;
  }

  public void RemoveAdjustment(PayrollAdjustmentId adjustmentId)
  {
    var adjustment = _adjustments.FirstOrDefault(a => a.Id == adjustmentId)
      ?? throw new DomainException("The adjustment does not belong to this pay slip.");
    _adjustments.Remove(adjustment);
    RecomputeTotals();
  }

  /// Holds the salary back (e.g. pending an inquiry): no payment is made while held. Allowed after finalization.
  public void Hold(string? remarks)
  {
    if (IsHeld)
      throw new DomainException("This salary is already held.");

    Status = PayrollTransactionStatus.Held;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  public void Release(string? remarks)
  {
    if (!IsHeld)
      throw new DomainException("This salary is not held.");

    Status = PayrollTransactionStatus.Released;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  private void RecomputeTotals()
  {
    var earnings = _lines.Where(l => l.ComponentType == ComponentType.Earning).Sum(l => l.CalculatedAmount);
    var deductions = _lines.Where(l => l.ComponentType == ComponentType.Deduction).Sum(l => l.CalculatedAmount);

    GrossPay = earnings + _adjustments.Where(a => a.Amount > 0).Sum(a => a.Amount);
    TotalDeductions = deductions - _adjustments.Where(a => a.Amount < 0).Sum(a => a.Amount);
    NetPayable = GrossPay - TotalDeductions;
  }
}

public class PayrollTransactionSegment : Entity<PayrollSegmentId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public PostId PostId { get; private set; } = default!;
  public PayScaleGradeId GradeId { get; private set; } = default!;
  public PayScaleStageId? PayScaleStageId { get; private set; }
  public decimal BasicPay { get; private set; }
  public DateOnly PeriodFrom { get; private set; }
  public DateOnly PeriodTo { get; private set; }
  public decimal Days { get; private set; }

  internal static PayrollTransactionSegment Create(PayrollTransactionId transactionId, SegmentResult segment) => new()
  {
    Id = PayrollSegmentId.New(),
    PayrollTransactionId = transactionId,
    PostId = segment.PostId,
    GradeId = segment.GradeId,
    PayScaleStageId = segment.StageId,
    BasicPay = Guard.Money(segment.BasicPay, "Basic pay"),
    PeriodFrom = segment.From,
    PeriodTo = segment.To,
    Days = segment.Days
  };
}

public class PayrollLoanDeduction : Entity<PayrollLoanDeductionId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public EmployeeLoanId EmployeeLoanId { get; private set; } = default!;
  public LoanInstallmentId InstallmentId { get; private set; } = default!;
  public decimal InstallmentAmount { get; private set; }

  internal static PayrollLoanDeduction Create(PayrollTransactionId transactionId, LoanDeductionResult loan) => new()
  {
    Id = PayrollLoanDeductionId.New(),
    PayrollTransactionId = transactionId,
    EmployeeLoanId = loan.LoanId,
    InstallmentId = loan.InstallmentId,
    InstallmentAmount = Guard.Money(Guard.Positive(loan.Amount, "Installment"), "Installment")
  };
}

/// One pay-slip line. Loan and advance deductions appear once with source "loan" and a link to the loan deduction,
/// which points at the exact installment.
public class PayrollComponentDetail : Entity<PayrollComponentLineId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public PayrollSegmentId? SegmentId { get; private set; }
  public SalaryComponentId SalaryComponentId { get; private set; } = default!;
  public SalaryComponentRuleId? SalaryComponentRuleId { get; private set; }
  public ComponentSource Source { get; private set; }
  public PayrollLoanDeductionId? PayrollLoanDeductionId { get; private set; }
  public ComponentType ComponentType { get; private set; }
  public string? CalculationBase { get; private set; }
  public decimal? BaseAmount { get; private set; }
  public decimal? Rate { get; private set; }
  public string? FormulaReference { get; private set; }
  public decimal CalculatedAmount { get; private set; }
  public string? NotificationRef { get; private set; }
  public DateOnly? EffectiveDate { get; private set; }

  internal static PayrollComponentDetail Create(PayrollTransactionId transactionId, PayrollSegmentId? segmentId, PayrollLoanDeductionId? loanDeductionId, LineResult line)
  {
    if ((line.Source == ComponentSource.Loan) != (loanDeductionId is not null))
      throw new DomainException("A loan line needs its loan deduction, and only a loan line has one.");
    if (line.Source == ComponentSource.Loan && line.ComponentType != ComponentType.Deduction)
      throw new DomainException("A loan line is a deduction.");

    return new PayrollComponentDetail
    {
      Id = PayrollComponentLineId.New(),
      PayrollTransactionId = transactionId,
      SegmentId = segmentId,
      SalaryComponentId = line.ComponentId,
      SalaryComponentRuleId = line.RuleId,
      Source = line.Source,
      PayrollLoanDeductionId = loanDeductionId,
      ComponentType = line.ComponentType,
      CalculationBase = line.CalculationBase,
      BaseAmount = line.BaseAmount is { } baseAmount ? decimal.Round(baseAmount, 2, MidpointRounding.AwayFromZero) : null,
      Rate = line.Rate,
      FormulaReference = line.FormulaReference,
      CalculatedAmount = Guard.Money(line.Amount, "Line amount"),
      NotificationRef = line.NotificationRef,
      EffectiveDate = line.EffectiveDate
    };
  }
}

/// A hand-entered correction on a pay slip. Signed: positive raises net pay (arrears, bonus), negative lowers it
/// (a recovery).
public class PayrollAdjustment : Entity<PayrollAdjustmentId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public AdjustmentType AdjustmentType { get; private set; }
  public decimal Amount { get; private set; }
  public string? Reason { get; private set; }

  internal static PayrollAdjustment Create(PayrollTransactionId transactionId, AdjustmentType type, decimal amount, string? reason)
  {
    amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    if (amount == 0)
      throw new DomainException("An adjustment of zero changes nothing.");
    if (type == AdjustmentType.Recovery && amount > 0)
      throw new DomainException("A recovery lowers net pay: enter it as a negative amount.");
    if ((type is AdjustmentType.Arrears or AdjustmentType.Bonus) && amount < 0)
      throw new DomainException($"{EnumText.Words(type)} raises net pay: enter it as a positive amount.");

    return new PayrollAdjustment
    {
      Id = PayrollAdjustmentId.New(),
      PayrollTransactionId = transactionId,
      AdjustmentType = type,
      Amount = amount,
      Reason = Guard.RequiredText(reason, 2000, "Reason")
    };
  }
}

/// The pay slip as issued: a frozen JSON snapshot taken when the run was finalized. It never changes afterwards
/// (a trigger refuses it); only a file reference (e.g. a printed PDF) may be added later.
public class Payslip : Aggregate<PayslipId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public string SnapshotJson { get; private set; } = default!;
  public DateTime GeneratedAt { get; private set; }
  public string? FileReference { get; private set; }

  public static Payslip Issue(PayrollTransactionId transactionId, string snapshotJson, DateTime generatedAt) => new()
  {
    Id = PayslipId.New(),
    PayrollTransactionId = transactionId,
    SnapshotJson = Guard.RequiredText(snapshotJson, int.MaxValue, "Pay slip"),
    GeneratedAt = generatedAt
  };

  public void AttachFile(string fileReference) => FileReference = Guard.RequiredText(fileReference, 500, "File");
}

/// Payment of a finalized pay slip into the employee's bank account. The destination is copied when the payment is
/// made, so later changes to the account never rewrite it. At most one live (pending or processed) payment per pay slip.
public class PayrollPayment : Aggregate<PayrollPaymentId>
{
  public PayrollTransactionId PayrollTransactionId { get; private set; } = default!;
  public EmployeeId EmployeeId { get; private set; } = default!;
  public BankAccountId BankAccountId { get; private set; } = default!;
  public string? BankNameSnapshot { get; private set; }
  public string? BranchNameSnapshot { get; private set; }
  public string? AccountNumberSnapshot { get; private set; }
  public string? IbanSnapshot { get; private set; }
  public PaymentMethod PaymentMethod { get; private set; }
  public PaymentStatus PaymentStatus { get; private set; }
  public DateOnly? PaymentDate { get; private set; }
  public string? PaymentReference { get; private set; }

  public bool IsLive => PaymentStatus is PaymentStatus.Pending or PaymentStatus.Processed;

  public static PayrollPayment Create(PayrollPaymentId id, PayrollRun run, PayrollTransaction transaction, EmployeeBankAccount account, PaymentMethod method)
  {
    ArgumentNullException.ThrowIfNull(run);
    ArgumentNullException.ThrowIfNull(transaction);
    ArgumentNullException.ThrowIfNull(account);

    if (run.Id != transaction.PayrollRunId)
      throw new DomainException("The pay slip does not belong to this run.");
    if (run.Status is not (PayrollRunStatus.Finalized or PayrollRunStatus.Paid))
      throw new DomainException("Salaries are paid only from a finalized run.");
    if (transaction.IsHeld)
      throw new DomainException("This salary is held; release it before paying.");
    if (transaction.NetPayable <= 0)
      throw new DomainException("There is nothing to pay on this pay slip.");
    if (account.EmployeeId != transaction.EmployeeId)
      throw new DomainException("The bank account belongs to a different employee.");
    if (!account.IsUsable)
      throw new DomainException("The bank account is inactive.");

    return new PayrollPayment
    {
      Id = id,
      PayrollTransactionId = transaction.Id,
      EmployeeId = transaction.EmployeeId,
      BankAccountId = account.Id,
      BankNameSnapshot = account.BankName,
      BranchNameSnapshot = account.BranchName,
      AccountNumberSnapshot = account.AccountNumber,
      IbanSnapshot = account.Iban,
      PaymentMethod = method,
      PaymentStatus = PaymentStatus.Pending
    };
  }

  public void Process(DateOnly paymentDate, string? paymentReference)
  {
    if (PaymentStatus != PaymentStatus.Pending)
      throw new DomainException($"The payment is {EnumText.Words(PaymentStatus)}; only a pending one can be marked processed.");

    PaymentReference = Guard.RequiredText(paymentReference, 100, "Bank reference");
    PaymentDate = paymentDate;
    PaymentStatus = PaymentStatus.Processed;
  }

  /// The bank refused the payment before it went out.
  public void Fail(string? paymentReference)
  {
    if (PaymentStatus != PaymentStatus.Pending)
      throw new DomainException($"The payment is {EnumText.Words(PaymentStatus)}; only a pending one can fail.");

    PaymentReference = Guard.Text(paymentReference, 100, "Bank reference") ?? PaymentReference;
    PaymentStatus = PaymentStatus.Failed;
  }

  /// The money came back after it was sent (closed account, wrong title).
  public void MarkReturned(string? paymentReference)
  {
    if (PaymentStatus != PaymentStatus.Processed)
      throw new DomainException("Only a processed payment can come back.");

    PaymentReference = Guard.Text(paymentReference, 100, "Bank reference") ?? PaymentReference;
    PaymentStatus = PaymentStatus.Returned;
  }
}
