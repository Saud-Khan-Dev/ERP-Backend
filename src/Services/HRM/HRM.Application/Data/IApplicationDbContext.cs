using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public interface IApplicationDbContext
{
  // ---- 1. organization ----
  DbSet<OrganizationUnitType> OrganizationUnitTypes { get; }
  DbSet<Location> Locations { get; }
  DbSet<OrganizationUnit> OrganizationUnits { get; }
  DbSet<OrganizationUnitVersion> OrganizationUnitVersions { get; }

  // ---- 2-3. designation, pay scale, posts ----
  DbSet<Designation> Designations { get; }
  DbSet<PayScaleGrade> PayScaleGrades { get; }
  DbSet<PayScaleVersion> PayScaleVersions { get; }
  DbSet<PayScaleStage> PayScaleStages { get; }
  DbSet<Post> Posts { get; }
  DbSet<PostVersion> PostVersions { get; }

  // ---- 4. employee master ----
  DbSet<Employee> Employees { get; }
  DbSet<EmployeeContact> EmployeeContacts { get; }
  DbSet<EmployeeAddress> EmployeeAddresses { get; }
  DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts { get; }
  DbSet<EmployeeFamilyMember> EmployeeFamilyMembers { get; }
  DbSet<EmployeeBankAccount> BankAccounts { get; }
  DbSet<EmployeePayRecord> PayRecords { get; }

  // ---- 5-6. documents, education, recruitment ----
  DbSet<DocumentType> DocumentTypes { get; }
  DbSet<EmployeeDocument> Documents { get; }
  DbSet<EmployeeEducation> Educations { get; }
  DbSet<RecruitmentMethod> RecruitmentMethods { get; }

  // ---- 7. service ----
  DbSet<ServiceEventType> ServiceEventTypes { get; }
  DbSet<EmployeeServiceHistory> ServiceHistory { get; }
  DbSet<PositionAssignment> PositionAssignments { get; }
  DbSet<HrActionRequest> HrActions { get; }
  DbSet<EmployeeSeparation> Separations { get; }

  // ---- 8. performance ----
  DbSet<PerformancePeriod> PerformancePeriods { get; }
  DbSet<PerformanceReview> PerformanceReviews { get; }

  // ---- 9. attendance ----
  DbSet<WorkShift> WorkShifts { get; }
  DbSet<EmployeeShift> EmployeeShifts { get; }
  DbSet<Holiday> Holidays { get; }
  DbSet<AttendanceRecord> AttendanceRecords { get; }

  // ---- 10. leave ----
  DbSet<LeaveType> LeaveTypes { get; }
  DbSet<LeaveEntitlement> LeaveEntitlements { get; }
  DbSet<LeaveApplication> LeaveApplications { get; }
  DbSet<LeaveLedgerEntry> LeaveLedger { get; }

  // ---- 11-12. salary structure, tax ----
  DbSet<SalaryComponent> SalaryComponents { get; }
  DbSet<SalaryComponentRule> SalaryRules { get; }
  DbSet<EmployeeSalaryComponent> SalaryOverrides { get; }
  DbSet<TaxYear> TaxYears { get; }
  DbSet<EmployeeTaxExemption> TaxExemptions { get; }
  DbSet<EmployeeTaxLedgerEntry> TaxLedger { get; }

  // ---- 13. loans, GP Fund ----
  DbSet<LoanType> LoanTypes { get; }
  DbSet<EmployeeLoan> Loans { get; }
  DbSet<LoanInstallment> LoanInstallments { get; }
  DbSet<GpFundAccount> GpFundAccounts { get; }
  DbSet<GpFundInterestRate> GpFundInterestRates { get; }
  DbSet<GpFundTransaction> GpFundTransactions { get; }

  // ---- 14-15. payroll, payment ----
  DbSet<PayrollPeriod> PayrollPeriods { get; }
  DbSet<PayrollRun> PayrollRuns { get; }
  DbSet<PayrollTransaction> PayrollTransactions { get; }
  DbSet<PayrollTransactionSegment> PayrollSegments { get; }
  DbSet<PayrollComponentDetail> PayrollLines { get; }
  DbSet<PayrollLoanDeduction> PayrollLoanDeductions { get; }
  DbSet<PayrollAdjustment> PayrollAdjustments { get; }
  DbSet<Payslip> Payslips { get; }
  DbSet<PayrollPayment> Payments { get; }

  // ---- 16-17. tasks, requests ----
  DbSet<EmployeeTask> Tasks { get; }
  DbSet<EmployeeRequestType> RequestTypes { get; }
  DbSet<EmployeeRequest> Requests { get; }

  // ---- views (read-only) ----
  DbSet<OrganizationUnitCurrent> CurrentOrganizationUnits { get; }
  DbSet<PostOccupancy> PostOccupancies { get; }
  DbSet<LeaveBalance> LeaveBalances { get; }
  DbSet<GpFundBalance> GpFundBalances { get; }
  DbSet<EmployeeTaxYtd> EmployeeTaxYtd { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);

  /// For the few use cases that must save in steps inside one transaction.
  Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
