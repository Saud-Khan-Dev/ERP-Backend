using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; this service owns the "hrms" schema.
  public const string Schema = "hrms";

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

  // ---- 1. organization ----
  public DbSet<OrganizationUnitType> OrganizationUnitTypes => Set<OrganizationUnitType>();
  public DbSet<Location> Locations => Set<Location>();
  public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
  public DbSet<OrganizationUnitVersion> OrganizationUnitVersions => Set<OrganizationUnitVersion>();

  // ---- 2-3. designation, pay scale, posts ----
  public DbSet<Designation> Designations => Set<Designation>();
  public DbSet<PayScaleGrade> PayScaleGrades => Set<PayScaleGrade>();
  public DbSet<PayScaleVersion> PayScaleVersions => Set<PayScaleVersion>();
  public DbSet<PayScaleStage> PayScaleStages => Set<PayScaleStage>();
  public DbSet<Post> Posts => Set<Post>();
  public DbSet<PostVersion> PostVersions => Set<PostVersion>();

  // ---- 4. employee master ----
  public DbSet<Employee> Employees => Set<Employee>();
  public DbSet<EmployeeContact> EmployeeContacts => Set<EmployeeContact>();
  public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
  public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
  public DbSet<EmployeeFamilyMember> EmployeeFamilyMembers => Set<EmployeeFamilyMember>();
  public DbSet<EmployeeBankAccount> BankAccounts => Set<EmployeeBankAccount>();
  public DbSet<EmployeePayRecord> PayRecords => Set<EmployeePayRecord>();

  // ---- 5-6. documents, education, recruitment ----
  public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
  public DbSet<EmployeeDocument> Documents => Set<EmployeeDocument>();
  public DbSet<EmployeeEducation> Educations => Set<EmployeeEducation>();
  public DbSet<RecruitmentMethod> RecruitmentMethods => Set<RecruitmentMethod>();

  // ---- 7. service ----
  public DbSet<ServiceEventType> ServiceEventTypes => Set<ServiceEventType>();
  public DbSet<EmployeeServiceHistory> ServiceHistory => Set<EmployeeServiceHistory>();
  public DbSet<PositionAssignment> PositionAssignments => Set<PositionAssignment>();
  public DbSet<HrActionRequest> HrActions => Set<HrActionRequest>();
  public DbSet<EmployeeSeparation> Separations => Set<EmployeeSeparation>();

  // ---- 8. performance ----
  public DbSet<PerformancePeriod> PerformancePeriods => Set<PerformancePeriod>();
  public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();

  // ---- 9. attendance ----
  public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
  public DbSet<EmployeeShift> EmployeeShifts => Set<EmployeeShift>();
  public DbSet<Holiday> Holidays => Set<Holiday>();
  public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

  // ---- 10. leave ----
  public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
  public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();
  public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
  public DbSet<LeaveLedgerEntry> LeaveLedger => Set<LeaveLedgerEntry>();

  // ---- 11-12. salary structure, tax ----
  public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
  public DbSet<SalaryComponentRule> SalaryRules => Set<SalaryComponentRule>();
  public DbSet<EmployeeSalaryComponent> SalaryOverrides => Set<EmployeeSalaryComponent>();
  public DbSet<TaxYear> TaxYears => Set<TaxYear>();
  public DbSet<EmployeeTaxExemption> TaxExemptions => Set<EmployeeTaxExemption>();
  public DbSet<EmployeeTaxLedgerEntry> TaxLedger => Set<EmployeeTaxLedgerEntry>();

  // ---- 13. loans, GP Fund ----
  public DbSet<LoanType> LoanTypes => Set<LoanType>();
  public DbSet<EmployeeLoan> Loans => Set<EmployeeLoan>();
  public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();
  public DbSet<GpFundAccount> GpFundAccounts => Set<GpFundAccount>();
  public DbSet<GpFundInterestRate> GpFundInterestRates => Set<GpFundInterestRate>();
  public DbSet<GpFundTransaction> GpFundTransactions => Set<GpFundTransaction>();

  // ---- 14-15. payroll, payment ----
  public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
  public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
  public DbSet<PayrollTransaction> PayrollTransactions => Set<PayrollTransaction>();
  public DbSet<PayrollTransactionSegment> PayrollSegments => Set<PayrollTransactionSegment>();
  public DbSet<PayrollComponentDetail> PayrollLines => Set<PayrollComponentDetail>();
  public DbSet<PayrollLoanDeduction> PayrollLoanDeductions => Set<PayrollLoanDeduction>();
  public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
  public DbSet<Payslip> Payslips => Set<Payslip>();
  public DbSet<PayrollPayment> Payments => Set<PayrollPayment>();

  // ---- 16-17. tasks, requests ----
  public DbSet<EmployeeTask> Tasks => Set<EmployeeTask>();
  public DbSet<EmployeeRequestType> RequestTypes => Set<EmployeeRequestType>();
  public DbSet<EmployeeRequest> Requests => Set<EmployeeRequest>();

  // ---- views ----
  public DbSet<OrganizationUnitCurrent> CurrentOrganizationUnits => Set<OrganizationUnitCurrent>();
  public DbSet<PostOccupancy> PostOccupancies => Set<PostOccupancy>();
  public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
  public DbSet<GpFundBalance> GpFundBalances => Set<GpFundBalance>();
  public DbSet<EmployeeTaxYtd> EmployeeTaxYtd => Set<EmployeeTaxYtd>();

  public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
      Database.BeginTransactionAsync(cancellationToken);

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    // every strongly typed id is a uuid column
    var idTypes = typeof(EmployeeId).Assembly.GetTypes()
      .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<>)));
    foreach (var idType in idTypes)
      configurationBuilder.Properties(idType).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(idType));

    // the schema's strings are unbounded varchar (text only where it says text); money is numeric(14,2)
    configurationBuilder.Properties<string>().HaveColumnType("varchar");
    configurationBuilder.Properties<decimal>().HavePrecision(14, 2);

    // the schema names every index itself (see the configurations), so EF must not add its own per foreign key
    configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    builder.HasDefaultSchema(Schema);
    builder.HasPostgresExtension("btree_gist");
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    foreach (var entity in builder.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() is not null))
    {
      var table = entity.GetTableName()!;

      // Postgres' own names: <table>_pkey and <table>_<columns>_fkey, unless a configuration named it
      entity.FindPrimaryKey()!.SetName($"{table}_pkey");
      foreach (var foreignKey in entity.GetForeignKeys())
      {
        if (((IConventionForeignKey)foreignKey).GetConstraintNameConfigurationSource() != ConfigurationSource.Explicit)
          foreignKey.SetConstraintName(Truncate($"{table}_{string.Join("_", foreignKey.Properties.Select(p => p.GetColumnName()))}_fkey"));
      }

      // The domain always sets every value, so EF always sends it; the database defaults are there for SQL written
      // by hand. Without this EF would leave out a false / 0 / first-enum value and let the default replace it.
      foreach (var property in entity.GetProperties())
      {
        if (property.GetComputedColumnSql() is null && (property.GetDefaultValueSql() is not null || property.TryGetDefaultValue(out _)))
          property.ValueGenerated = ValueGenerated.Never;
      }
    }

    base.OnModelCreating(builder);
  }

  /// Postgres cuts identifiers at 63 bytes.
  private static string Truncate(string name) => name.Length <= 63 ? name : name[..63];
}
