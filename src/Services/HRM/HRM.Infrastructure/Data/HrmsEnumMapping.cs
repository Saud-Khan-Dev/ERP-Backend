using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

/// Every C# enum mapped to its native Postgres enum type in the hrms schema. Member names become snake_case labels
/// (DeputedOut -> deputed_out) through Npgsql's default name translator.
public static class HrmsEnumMapping
{
  public static void Apply(NpgsqlDbContextOptionsBuilder npgsql)
  {
    const string schema = ApplicationDbContext.Schema;

    npgsql.MapEnum<Gender>("gender_enum", schema);
    npgsql.MapEnum<MaritalStatus>("marital_status_enum", schema);
    npgsql.MapEnum<EmploymentStatus>("employment_status_enum", schema);
    npgsql.MapEnum<EmploymentType>("employment_type_enum", schema);
    npgsql.MapEnum<EmploymentMethod>("employment_method_enum", schema);
    npgsql.MapEnum<PostLifecycle>("post_lifecycle_enum", schema);
    npgsql.MapEnum<PositionStatus>("position_status_enum", schema);
    npgsql.MapEnum<AssignmentType>("assignment_type_enum", schema);
    npgsql.MapEnum<ComponentType>("component_type_enum", schema);
    npgsql.MapEnum<CalculationMethod>("calculation_method_enum", schema);
    npgsql.MapEnum<ComponentSource>("component_source_enum", schema);
    npgsql.MapEnum<PayrollRunStatus>("payroll_run_status_enum", schema);
    npgsql.MapEnum<PayrollRunType>("payroll_run_type_enum", schema);
    npgsql.MapEnum<PayrollPeriodStatus>("payroll_period_status_enum", schema);
    npgsql.MapEnum<PayrollTransactionStatus>("payroll_txn_status_enum", schema);
    npgsql.MapEnum<AdjustmentType>("adjustment_type_enum", schema);
    npgsql.MapEnum<PaymentStatus>("payment_status_enum", schema);
    npgsql.MapEnum<PaymentMethod>("payment_method_enum", schema);
    npgsql.MapEnum<VerificationStatus>("verification_status_enum", schema);
    npgsql.MapEnum<RecordStatus>("record_status_enum", schema);
    npgsql.MapEnum<TaskPriority>("task_priority_enum", schema);
    npgsql.MapEnum<EmployeeTaskStatus>("task_status_enum", schema);
    npgsql.MapEnum<EmployeeRequestStatus>("employee_request_status_enum", schema);
    npgsql.MapEnum<ContactType>("contact_type_enum", schema);
    npgsql.MapEnum<AddressType>("address_type_enum", schema);
    npgsql.MapEnum<QualificationLevel>("qualification_level_enum", schema);
    npgsql.MapEnum<SeparationType>("separation_type_enum", schema);
    npgsql.MapEnum<HrActionType>("hr_action_type_enum", schema);
    npgsql.MapEnum<HrActionStatus>("hr_action_status_enum", schema);
    npgsql.MapEnum<ReviewStatus>("review_status_enum", schema);
    npgsql.MapEnum<AttendanceStatus>("attendance_status_enum", schema);
    npgsql.MapEnum<HolidayType>("holiday_type_enum", schema);
    npgsql.MapEnum<LeaveStatus>("leave_status_enum", schema);
    npgsql.MapEnum<LeaveTransactionType>("leave_txn_type_enum", schema);
    npgsql.MapEnum<LoanStatus>("loan_status_enum", schema);
    npgsql.MapEnum<InstallmentStatus>("installment_status_enum", schema);
    npgsql.MapEnum<GpFundTransactionType>("gpf_txn_type_enum", schema);
  }
}
