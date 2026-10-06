using Microsoft.EntityFrameworkCore;

/// Load a tracked aggregate by id or throw the matching 404, so handlers read as one line each.
public static class EntityLoaders
{
  public static async Task<Employee> LoadEmployeeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken, bool withDetails = false)
  {
    var employeeId = EmployeeId.Of(id);
    var query = context.Employees.AsQueryable();
    if (withDetails)
      query = query.Include(e => e.Contacts).Include(e => e.Addresses).Include(e => e.EmergencyContacts).Include(e => e.FamilyMembers);

    return await query.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
      ?? throw new EmployeeNotFoundException($"Employee {id} was not found.");
  }

  public static async Task<OrganizationUnit> LoadOrgUnitAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var unitId = OrganizationUnitId.Of(id);
    return await context.OrganizationUnits.Include(u => u.Versions).FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken)
      ?? throw new OrganizationUnitNotFoundException($"Org unit {id} was not found.");
  }

  public static async Task<Post> LoadPostAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var postId = PostId.Of(id);
    return await context.Posts.Include(p => p.Versions).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken)
      ?? throw new PostNotFoundException($"Post {id} was not found.");
  }

  public static async Task<PayScaleVersion> LoadPayScaleVersionAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var versionId = PayScaleVersionId.Of(id);
    return await context.PayScaleVersions.Include(v => v.Stages).FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken)
      ?? throw new PayScaleVersionNotFoundException($"Pay scale {id} was not found.");
  }

  public static async Task<PayScaleGrade> LoadGradeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var gradeId = PayScaleGradeId.Of(id);
    return await context.PayScaleGrades.FirstOrDefaultAsync(g => g.Id == gradeId, cancellationToken)
      ?? throw new PayScaleGradeNotFoundException($"Grade {id} was not found.");
  }

  public static async Task<Designation> LoadDesignationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var designationId = DesignationId.Of(id);
    return await context.Designations.FirstOrDefaultAsync(d => d.Id == designationId, cancellationToken)
      ?? throw new DesignationNotFoundException($"Designation {id} was not found.");
  }

  public static async Task<Location> LoadLocationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var locationId = LocationId.Of(id);
    return await context.Locations.FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken)
      ?? throw new LocationNotFoundException($"Location {id} was not found.");
  }

  public static async Task<OrganizationUnitType> LoadUnitTypeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var typeId = OrganizationUnitTypeId.Of(id);
    return await context.OrganizationUnitTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new OrganizationUnitTypeNotFoundException($"Unit type {id} was not found.");
  }

  public static async Task<EmployeeDocument> LoadDocumentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var documentId = EmployeeDocumentId.Of(id);
    return await context.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
      ?? throw new EmployeeDocumentNotFoundException($"Document {id} was not found.");
  }

  public static async Task<DocumentType> LoadDocumentTypeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var typeId = DocumentTypeId.Of(id);
    return await context.DocumentTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new DocumentTypeNotFoundException($"Document type {id} was not found.");
  }

  public static async Task<ServiceEventType> LoadEventTypeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var typeId = ServiceEventTypeId.Of(id);
    return await context.ServiceEventTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new ServiceEventTypeNotFoundException($"Service event type {id} was not found.");
  }

  /// The event type the code records under, by its fixed name (the seeder keeps these present).
  public static async Task<ServiceEventType> LoadEventTypeAsync(this IApplicationDbContext context, string name, CancellationToken cancellationToken) =>
      await context.ServiceEventTypes.FirstOrDefaultAsync(t => t.Name == name, cancellationToken)
      ?? throw new ServiceEventTypeNotFoundException($"The service event type '{name}' is missing. Restart the HRM service to restore it.");

  public static async Task<HrActionRequest> LoadHrActionAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var actionId = HrActionRequestId.Of(id);
    return await context.HrActions.FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
      ?? throw new HrActionNotFoundException($"HR action {id} was not found.");
  }

  public static async Task<PositionAssignment> LoadAssignmentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var assignmentId = PositionAssignmentId.Of(id);
    return await context.PositionAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
      ?? throw new PositionAssignmentNotFoundException($"Assignment {id} was not found.");
  }

  public static async Task<WorkShift> LoadShiftAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var shiftId = WorkShiftId.Of(id);
    return await context.WorkShifts.FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
      ?? throw new WorkShiftNotFoundException($"Shift {id} was not found.");
  }

  public static async Task<LeaveType> LoadLeaveTypeAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var typeId = LeaveTypeId.Of(id);
    return await context.LeaveTypes.FirstOrDefaultAsync(t => t.Id == typeId, cancellationToken)
      ?? throw new LeaveTypeNotFoundException($"Leave type {id} was not found.");
  }

  public static async Task<LeaveApplication> LoadLeaveApplicationAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var applicationId = LeaveApplicationId.Of(id);
    return await context.LeaveApplications.FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
      ?? throw new LeaveApplicationNotFoundException($"Leave application {id} was not found.");
  }

  public static async Task<SalaryComponent> LoadComponentAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var componentId = SalaryComponentId.Of(id);
    return await context.SalaryComponents.FirstOrDefaultAsync(c => c.Id == componentId, cancellationToken)
      ?? throw new SalaryComponentNotFoundException($"Salary component {id} was not found.");
  }

  public static async Task<TaxYear> LoadTaxYearAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var yearId = TaxYearId.Of(id);
    return await context.TaxYears.Include(t => t.Slabs).FirstOrDefaultAsync(t => t.Id == yearId, cancellationToken)
      ?? throw new TaxYearNotFoundException($"Tax year {id} was not found.");
  }

  public static async Task<EmployeeLoan> LoadLoanAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var loanId = EmployeeLoanId.Of(id);
    return await context.Loans.Include(l => l.Installments).FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken)
      ?? throw new LoanNotFoundException($"Loan {id} was not found.");
  }

  public static async Task<GpFundAccount> LoadGpFundAccountAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var accountId = GpFundAccountId.Of(id);
    return await context.GpFundAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
      ?? throw new GpFundAccountNotFoundException($"GP Fund account {id} was not found.");
  }

  public static async Task<PayrollPeriod> LoadPayrollPeriodAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var periodId = PayrollPeriodId.Of(id);
    return await context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
      ?? throw new PayrollPeriodNotFoundException($"Payroll period {id} was not found.");
  }

  public static async Task<PayrollRun> LoadPayrollRunAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var runId = PayrollRunId.Of(id);
    return await context.PayrollRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
      ?? throw new PayrollRunNotFoundException($"Payroll run {id} was not found.");
  }

  public static async Task<PayrollTransaction> LoadPayrollTransactionAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var transactionId = PayrollTransactionId.Of(id);
    return await context.PayrollTransactions
        .Include(t => t.Segments).Include(t => t.Lines).Include(t => t.LoanDeductions).Include(t => t.Adjustments)
        .AsSplitQuery()
        .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken)
      ?? throw new PayrollTransactionNotFoundException($"Pay slip {id} was not found.");
  }

  public static async Task<EmployeeTask> LoadTaskAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var taskId = EmployeeTaskId.Of(id);
    return await context.Tasks.Include(t => t.Updates).FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
      ?? throw new TaskNotFoundException($"Task {id} was not found.");
  }

  public static async Task<EmployeeRequest> LoadRequestAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var requestId = EmployeeRequestId.Of(id);
    return await context.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
      ?? throw new EmployeeRequestNotFoundException($"Request {id} was not found.");
  }

  public static async Task<PerformancePeriod> LoadPerformancePeriodAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var periodId = PerformancePeriodId.Of(id);
    return await context.PerformancePeriods.FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken)
      ?? throw new PerformancePeriodNotFoundException($"Performance period {id} was not found.");
  }

  public static async Task<PerformanceReview> LoadReviewAsync(this IApplicationDbContext context, Guid id, CancellationToken cancellationToken)
  {
    var reviewId = PerformanceReviewId.Of(id);
    return await context.PerformanceReviews.Include(r => r.Goals).Include(r => r.Kpis).Include(r => r.Competencies)
        .AsSplitQuery()
        .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
      ?? throw new PerformanceReviewNotFoundException($"Performance review {id} was not found.");
  }
}
