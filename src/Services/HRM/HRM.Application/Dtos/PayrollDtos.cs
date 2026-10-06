public sealed record PayrollPeriodDto(Guid Id, int Year, int Month, string Label, DateOnly StartDate, DateOnly EndDate, PayrollPeriodStatus Status, int Runs);

public sealed record PayrollRunDto(
  Guid Id,
  Guid PayrollPeriodId,
  string Period,
  PayrollRunType RunType,
  string? RunLabel,
  PayrollRunStatus Status,
  Guid? ReversesRunId,
  Guid? PreparedBy,
  Guid? ReviewedBy,
  Guid? ApprovedBy,
  DateTime? ApprovalDate,
  DateTime? FinalizationDate,
  DateTime? PaymentDate,
  int Employees,
  int Held,
  decimal GrossPay,
  decimal TotalDeductions,
  decimal NetPayable,
  DateTime? CreatedAt);

public sealed record PayrollTransactionSummaryDto(
  Guid Id,
  Guid PayrollRunId,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  decimal? DaysPayable,
  decimal GrossPay,
  decimal TotalDeductions,
  decimal NetPayable,
  PayrollTransactionStatus Status,
  PaymentStatus? PaymentStatus,
  string? Remarks);

public sealed record PayrollSegmentDto(Guid Id, Guid PostId, string? PostCode, Guid GradeId, int? Bps, Guid? PayScaleStageId, decimal BasicPay, DateOnly PeriodFrom, DateOnly PeriodTo, decimal Days);

public sealed record PayrollLineDto(
  Guid Id,
  Guid? SegmentId,
  Guid SalaryComponentId,
  string ComponentCode,
  string ComponentName,
  Guid? SalaryComponentRuleId,
  ComponentSource Source,
  ComponentType ComponentType,
  string? CalculationBase,
  decimal? BaseAmount,
  decimal? Rate,
  string? FormulaReference,
  decimal CalculatedAmount,
  string? NotificationRef,
  DateOnly? EffectiveDate);

public sealed record PayrollLoanDeductionDto(Guid Id, Guid EmployeeLoanId, Guid InstallmentId, decimal InstallmentAmount);

public sealed record PayrollAdjustmentDto(Guid Id, AdjustmentType AdjustmentType, decimal Amount, string? Reason, Guid? CreatedBy, DateTime? CreatedAt);

public sealed record PayrollTransactionDto(
  PayrollTransactionSummaryDto Transaction,
  IReadOnlyList<PayrollSegmentDto> Segments,
  IReadOnlyList<PayrollLineDto> Lines,
  IReadOnlyList<PayrollLoanDeductionDto> LoanDeductions,
  IReadOnlyList<PayrollAdjustmentDto> Adjustments);

public sealed record PaymentDto(
  Guid Id,
  Guid PayrollTransactionId,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  decimal Amount,
  string? BankName,
  string? BranchName,
  string? AccountNumber,
  string? Iban,
  PaymentMethod PaymentMethod,
  PaymentStatus PaymentStatus,
  DateOnly? PaymentDate,
  string? PaymentReference,
  DateTime? CreatedAt);

// ---- pay slip (the frozen snapshot of a finalized slip, or a preview before that) ----

public sealed record PayslipLineDto(string ComponentCode, string ComponentName, decimal Amount, string? Detail);

public sealed record PayslipLoanDto(Guid EmployeeLoanId, string LoanType, string Installment, decimal Amount, decimal BalanceAfter);

public sealed record PayslipDto(
  Guid PayrollTransactionId,
  Guid PayrollRunId,
  string Period,
  int Year,
  int Month,
  PayrollRunType RunType,
  string? RunLabel,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  string Cnic,
  string? PostCode,
  string? Designation,
  int? Bps,
  int? Stage,
  string? OrgUnit,
  decimal? DaysPayable,
  int DaysInPeriod,
  IReadOnlyList<PayslipLineDto> Earnings,
  IReadOnlyList<PayslipLineDto> Deductions,
  decimal GrossPay,
  decimal TotalDeductions,
  decimal NetPayable,
  IReadOnlyList<PayslipLoanDto> Loans,
  decimal? GpFundBalance,
  string? TaxYear,
  decimal? TaxableIncomeYtd,
  decimal? TaxWithheldYtd,
  string? BankName,
  string? AccountNumber,
  string? Iban,
  string? Remarks,
  bool IsFinal,
  DateTime? IssuedAt);
