/// How HRM's records read in the audit trail (BuildingBlock/Audit): kinds in plain words, and every record about one
/// employee hangs under the employee, so the employee's History lists it. Records that hang under another record
/// (a loan's repayments, a pay slip's adjustments, a review's goals) hang under that record; the employee's History
/// endpoint gathers them too. Rows a run writes in bulk are skipped (the run records one line).
public static class HrmAuditProfile
{
  public const string Module = "Human Resources";

  public static void Configure(AuditProfile profile)
  {
    profile.Ignore("FullName", "PhotoReference", "SnapshotJson");

    // ---- organization and structure ----
    profile.Entity<OrganizationUnitType>(e => e.Kind("Unit type"));
    profile.Entity<OrganizationUnit>(e => e.Kind("Org unit").Code(u => u.Code)
      .Label(u => u.Versions.Count > 0 ? u.Latest.Name : null));
    profile.Entity<OrganizationUnitVersion>(e => e.Kind("Org unit version").Label(v => v.Name).Under<OrganizationUnit>(v => v.OrgUnitId)
      .Field("ParentUnitId", "Parent unit").Field("UnitTypeId", "Unit type").Field("HeadPostId", "Head post"));
    profile.Entity<Designation>(e => e.Kind("Designation"));
    profile.Entity<PayScaleGrade>(e => e.Kind("Grade").Label(g => g.Label));
    profile.Entity<PayScaleVersion>(e => e.Kind("Pay scale").Label(v => v.VersionLabel));
    profile.Entity<PayScaleStage>(e => e.Kind("Pay scale stage").Under<PayScaleVersion>(s => s.PayScaleVersionId));
    profile.Entity<Post>(e => e.Kind("Post").Code(p => p.PostCode));
    profile.Entity<PostVersion>(e => e.Kind("Post version").Under<Post>(v => v.PostId).Field("OrgUnitId", "Org unit"));

    // ---- the employee and their file ----
    profile.Entity<Employee>(e => e.Kind("Employee").Code(x => x.EmployeeNumber).Label(x => x.FullName ?? x.DisplayName)
      .Field("Cnic", "CNIC").Field("UserId", "Sign-in account"));
    profile.Entity<EmployeeContact>(e => e.Kind("Contact").Label(x => x.Value).Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeAddress>(e => e.Kind("Address").Under<Employee>(x => x.EmployeeId));
    profile.Entity<EngagementOrder>(e => e.Kind("Order").Under<Employee>(x => x.EmployeeId).Field("Rate", "Wage"));
    profile.Entity<EmployeeEmergencyContact>(e => e.Kind("Emergency contact").Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeFamilyMember>(e => e.Kind("Family member").Under<Employee>(x => x.EmployeeId).Field("Cnic", "CNIC"));
    profile.Entity<EmployeeBankAccount>(e => e.Kind("Bank account").Label(x => x.BankName).Under<Employee>(x => x.EmployeeId)
      .Field("Iban", "IBAN"));
    profile.Entity<EmployeePayRecord>(e => e.Kind("Pay record").Under<Employee>(x => x.EmployeeId).Field("PayScaleStageId", "Pay scale stage"));
    profile.Skip<EmployeeChangeLogEntry>();

    profile.Entity<DocumentType>(e => e.Kind("Document type"));
    profile.Entity<EmployeeDocument>(e => e.Kind("Document").Code(x => x.DocumentNumber).Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeEducation>(e => e.Kind("Education").Label(x => x.DegreeTitle).Under<Employee>(x => x.EmployeeId));
    profile.Entity<RecruitmentMethod>(e => e.Kind("Recruitment method"));
    profile.Entity<EmploymentType>(e => e.Kind("Employment type"));
    profile.Entity<QualificationLevel>(e => e.Kind("Qualification level"));
    profile.Entity<FamilyRelation>(e => e.Kind("Family relation"));
    profile.Entity<EmploymentMethod>(e => e.Kind("Employment method"));
    profile.Entity<AdjustmentType>(e => e.Kind("Adjustment type"));

    // ---- service ----
    profile.Entity<ServiceEventType>(e => e.Kind("Service event type"));
    profile.Entity<EmployeeServiceHistory>(e => e.Kind("Service record entry").Under<Employee>(x => x.EmployeeId));
    profile.Entity<PositionAssignment>(e => e.Kind("Post assignment").Under<Employee>(x => x.EmployeeId));
    profile.Entity<HrActionRequest>(e => e.Kind("HR action").Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeSeparation>(e => e.Kind("Separation").Under<Employee>(x => x.EmployeeId));

    // ---- performance ----

    // ---- attendance and leave ----
    profile.Entity<WorkShift>(e => e.Kind("Work shift"));
    profile.Entity<EmployeeShift>(e => e.Kind("Shift assignment").Under<Employee>(x => x.EmployeeId));
    profile.Entity<Holiday>(e => e.Kind("Holiday"));
    profile.Entity<AttendanceRecord>(e => e.Kind("Attendance").Label(x => x.AttendanceDate.ToString("yyyy-MM-dd")).Under<Employee>(x => x.EmployeeId));
    profile.Entity<LeaveType>(e => e.Kind("Leave type"));
    profile.Entity<LeaveEntitlement>(e => e.Kind("Leave entitlement").Under<Employee>(x => x.EmployeeId));
    profile.Entity<LeaveApplication>(e => e.Kind("Leave application").Under<Employee>(x => x.EmployeeId));
    profile.Entity<LeaveLedgerEntry>(e => e.Kind("Leave balance entry").Under<Employee>(x => x.EmployeeId));

    // ---- pay structure and tax ----
    profile.Entity<SalaryComponent>(e => e.Kind("Salary component").Code(x => x.ComponentCode).Label(x => x.ComponentName));
    profile.Entity<SalaryComponentRule>(e => e.Kind("Salary rule").Under<SalaryComponent>(x => x.SalaryComponentId));
    profile.Entity<EmployeeSalaryComponent>(e => e.Kind("Salary override").Under<Employee>(x => x.EmployeeId));
    profile.Entity<TaxYear>(e => e.Kind("Tax year").Label(x => x.YearLabel));
    profile.Entity<TaxSlab>(e => e.Kind("Tax slab").Under<TaxYear>(x => x.TaxYearId));
    profile.Entity<EmployeeTaxExemption>(e => e.Kind("Tax exemption").Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeTaxOpening>(e => e.Kind("Tax opening").Under<Employee>(x => x.EmployeeId));
    profile.Skip<EmployeeTaxLedgerEntry>();

    // ---- loans and GP Fund ----
    profile.Entity<LoanType>(e => e.Kind("Loan type"));
    profile.Entity<EmployeeLoan>(e => e.Kind("Loan").Under<Employee>(x => x.EmployeeId));
    profile.Entity<LoanRepayment>(e => e.Kind("Loan repayment").Code(x => x.ReceiptNumber).Under<EmployeeLoan>(x => x.EmployeeLoanId));
    profile.Skip<LoanInstallment>();
    profile.Entity<GpFundAccount>(e => e.Kind("GP Fund account").Under<Employee>(x => x.EmployeeId));
    profile.Entity<GpFundInterestRate>(e => e.Kind("GP Fund interest rate").Label(x => x.FiscalYear));
    profile.Entity<GpFundTransaction>(e => e.Kind("GP Fund entry").Under<GpFundAccount>(x => x.GpFundAccountId));

    // ---- payroll ----
    profile.Entity<PayrollPeriod>(e => e.Kind("Payroll month").Label(x => x.Label));
    profile.Entity<PayrollRun>(e => e.Kind("Payroll run").Label(x => x.RunLabel));
    profile.Entity<PayrollTransaction>(e => e.Kind("Pay slip").Under<Employee>(x => x.EmployeeId));
    profile.Entity<PayrollAdjustment>(e => e.Kind("Pay slip adjustment").Under<PayrollTransaction>(x => x.PayrollTransactionId));
    profile.Entity<PayrollPayment>(e => e.Kind("Salary payment").Under<Employee>(x => x.EmployeeId));
    profile.Skip<PayrollTransactionSegment>();
    profile.Skip<PayrollComponentDetail>();
    profile.Skip<PayrollLoanDeduction>();
    profile.Skip<Payslip>();

    // ---- tasks and requests ----
    profile.Entity<EmployeeTask>(e => e.Kind("Task").Under<Employee>(x => x.EmployeeId));
    profile.Entity<EmployeeTaskUpdate>(e => e.Kind("Task progress").Under<EmployeeTask>(x => x.TaskId));
    profile.Entity<EmployeeRequestType>(e => e.Kind("Request type"));
    profile.Entity<EmployeeRequest>(e => e.Kind("Request").Label(x => x.Subject).Under<Employee>(x => x.EmployeeId));
  }
}
