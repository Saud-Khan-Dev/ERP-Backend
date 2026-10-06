/// IsSystem: worked out by the payroll engine (basic pay, income tax, GP Fund); no rules or overrides.
public sealed record SalaryComponentDto(Guid Id, string ComponentCode, string ComponentName, ComponentType ComponentType, bool IsTaxable, bool IsSystem, bool IsActive);

/// IsUsed: already on a pay slip, so it can only be closed and replaced by a new version, not edited.
public sealed record SalaryRuleDto(
  Guid Id,
  Guid SalaryComponentId,
  string ComponentCode,
  string ComponentName,
  string RuleVersion,
  CalculationMethod CalculationMethod,
  decimal? FixedAmount,
  decimal? Percentage,
  string? CalculationBase,
  string? FormulaExpression,
  int? MinBps,
  int? MaxBps,
  Guid? ApplicableDesignationId,
  string? ApplicableDesignation,
  Guid? ApplicableOrgUnitId,
  string? ApplicableOrgUnit,
  EmploymentType? ApplicableEmploymentType,
  decimal? MinAmount,
  decimal? MaxAmount,
  int Priority,
  string? NotificationRef,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  RecordStatus Status,
  Guid? ApprovedBy,
  bool IsUsed);

public sealed record SalaryOverrideDto(
  Guid Id,
  Guid EmployeeId,
  Guid SalaryComponentId,
  string ComponentCode,
  string ComponentName,
  Guid? PostId,
  string? PostCode,
  decimal? OverrideFixedAmount,
  decimal? OverridePercentage,
  DateOnly EffectiveFrom,
  DateOnly? EffectiveTo,
  string? Remarks);

public sealed record TaxSlabDto(Guid Id, int SlabOrder, decimal MinIncome, decimal? MaxIncome, decimal FixedAmount, decimal RatePercentage);

public sealed record TaxYearDto(Guid Id, string YearLabel, DateOnly StartDate, DateOnly EndDate, RecordStatus Status, IReadOnlyList<TaxSlabDto> Slabs);

public sealed record TaxExemptionDto(Guid Id, Guid EmployeeId, Guid TaxYearId, string? YearLabel, string ExemptionType, decimal Amount);

public sealed record TaxLedgerEntryDto(Guid Id, Guid PayrollTransactionId, string? Period, PayrollRunType? RunType, decimal TaxableIncome, decimal TaxWithheld, DateTime? CreatedAt);

public sealed record LoanTypeDto(Guid Id, string Name, Guid? SalaryComponentId, string? SalaryComponent, bool IsGpfAdvance, decimal DefaultInterestRate, bool IsActive);

public sealed record LoanSummaryDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  Guid LoanTypeId,
  string? LoanType,
  decimal PrincipalAmount,
  decimal InterestAmount,
  int InstallmentsCount,
  decimal MonthlyInstallment,
  DateOnly StartDate,
  DateOnly? EndDate,
  decimal RemainingBalance,
  int DeductionPriority,
  LoanStatus Status,
  Guid? ApprovedBy,
  DateTime? CreatedAt);

public sealed record LoanInstallmentDto(Guid Id, int InstallmentNumber, DateOnly DueDate, decimal Amount, decimal PaidAmount, decimal Outstanding, InstallmentStatus Status);

/// Recovered: paid so far. Overdue: open installments already due.
public sealed record LoanDto(LoanSummaryDto Loan, decimal Recovered, decimal Overdue, IReadOnlyList<LoanInstallmentDto> Installments);

public sealed record GpFundAccountDto(
  Guid Id,
  Guid EmployeeId,
  string EmployeeNumber,
  string EmployeeName,
  string? AccountNumber,
  DateOnly OpenedOn,
  DateOnly? ClosedOn,
  decimal MonthlySubscription,
  RecordStatus Status,
  decimal Balance,
  decimal TotalSubscribed,
  decimal TotalInterest);

public sealed record GpFundTransactionDto(
  Guid Id,
  GpFundTransactionType TransactionType,
  DateOnly TransactionDate,
  decimal Amount,
  Guid? EmployeeLoanId,
  Guid? PayrollTransactionId,
  string? Remarks,
  Guid? CreatedBy,
  DateTime? CreatedAt);

public sealed record GpFundInterestRateDto(Guid Id, string FiscalYear, decimal RatePercent, string? NotificationRef, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed record BankAccountDto(
  Guid Id,
  Guid EmployeeId,
  string BankName,
  string? BranchName,
  string? AccountNumber,
  string? Iban,
  bool IsPrimary,
  RecordStatus Status,
  DateTime? CreatedAt);

public static class PayMappings
{
  public static SalaryComponentDto ToDto(this SalaryComponent x) =>
      new(x.Id.Value, x.ComponentCode, x.ComponentName, x.ComponentType, x.IsTaxable, x.IsSystem, x.IsActive);

  public static TaxYearDto ToDto(this TaxYear x) => new(x.Id.Value, x.YearLabel, x.StartDate, x.EndDate, x.Status,
    x.Slabs.Select(s => new TaxSlabDto(s.Id.Value, s.SlabOrder, s.MinIncome, s.MaxIncome, s.FixedAmount, s.RatePercentage)).ToList());

  public static LoanTypeDto ToDto(this LoanType x, string? componentName) =>
      new(x.Id.Value, x.Name, x.SalaryComponentId?.Value, componentName, x.IsGpfAdvance, x.DefaultInterestRate, x.IsActive);

  public static LoanInstallmentDto ToDto(this LoanInstallment x) =>
      new(x.Id.Value, x.InstallmentNumber, x.DueDate, x.Amount, x.PaidAmount, x.Outstanding, x.Status);

  public static GpFundTransactionDto ToDto(this GpFundTransaction x) => new(x.Id.Value, x.TransactionType, x.TransactionDate, x.Amount,
    x.EmployeeLoanId?.Value, x.PayrollTransactionId?.Value, x.Remarks, x.CreatedBy, x.CreatedAt);

  public static GpFundInterestRateDto ToDto(this GpFundInterestRate x) =>
      new(x.Id.Value, x.FiscalYear, x.RatePercent, x.NotificationRef, x.EffectiveFrom, x.EffectiveTo);

  public static BankAccountDto ToDto(this EmployeeBankAccount x) =>
      new(x.Id.Value, x.EmployeeId.Value, x.BankName, x.BranchName, x.AccountNumber, x.Iban, x.IsPrimary, x.Status, x.CreatedAt);
}
