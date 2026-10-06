using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 9-10: attendance, shifts, holidays; leave. leave_ledger is append-only (trigger).

public class WorkShiftConfiguration : EntityConfiguration<WorkShift, WorkShiftId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<WorkShift> builder)
  {
    base.Configure(builder);
    builder.ToTable("work_shift", t =>
    {
      t.HasCheckConstraint("work_shift_grace_minutes_check", "grace_minutes >= 0");
      t.HasCheckConstraint("ck_ws_days", "working_weekdays <@ ARRAY[1,2,3,4,5,6,7]::smallint[] AND cardinality(working_weekdays) > 0");
    });

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.GraceMinutes).HasDefaultValue(0);
    builder.Property(x => x.WorkingWeekdays).HasColumnType("smallint[]").HasDefaultValueSql("'{1,2,3,4,5}'::smallint[]");
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("work_shift_name_key");
  }
}

public class EmployeeShiftConfiguration : EntityConfiguration<EmployeeShift, EmployeeShiftId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<EmployeeShift> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_shift", t =>
      t.HasCheckConstraint("ck_es_dates", "effective_to IS NULL OR effective_to >= effective_from"));

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<WorkShift>().WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.NoAction);
    builder.HasIndex(x => x.WorkShiftId).HasDatabaseName("idx_employee_shift_work_shift_id");
  }
}

public class HolidayConfiguration : EntityConfiguration<Holiday, HolidayId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<Holiday> builder)
  {
    base.Configure(builder);
    builder.ToTable("holiday_calendar");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.HolidayType).HasDefaultValue(HolidayType.Public);

    builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);

    // two "everywhere" holidays with the same date and name clash even though location_id is NULL
    builder.HasIndex(x => new { x.HolidayDate, x.Name, x.LocationId }).IsUnique().AreNullsDistinct(false)
      .HasDatabaseName("holiday_calendar_holiday_date_name_location_id_key");
    builder.HasIndex(x => x.LocationId).HasDatabaseName("idx_holiday_calendar_location_id");
    builder.HasIndex(x => x.HolidayDate).HasDatabaseName("idx_holiday_date");
  }
}

public class AttendanceRecordConfiguration : EntityConfiguration<AttendanceRecord, AttendanceRecordId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt | AuditColumns.UpdatedAt;

  public override void Configure(EntityTypeBuilder<AttendanceRecord> builder)
  {
    base.Configure(builder);
    builder.ToTable("attendance_record", t =>
    {
      t.HasCheckConstraint("attendance_record_working_hours_check", "working_hours IS NULL OR working_hours BETWEEN 0 AND 24");
      t.HasCheckConstraint("attendance_record_overtime_hours_check", "overtime_hours IS NULL OR overtime_hours BETWEEN 0 AND 24");
      t.HasCheckConstraint("attendance_record_late_minutes_check", "late_minutes >= 0");
      t.HasCheckConstraint("attendance_record_early_departure_minutes_check", "early_departure_minutes >= 0");
    });

    builder.Property(x => x.WorkingHours).Numeric(5, 2);
    builder.Property(x => x.OvertimeHours).Numeric(5, 2);
    builder.Property(x => x.LateMinutes).HasDefaultValue(0);
    builder.Property(x => x.EarlyDepartureMinutes).HasDefaultValue(0);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<WorkShift>().WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.AttendanceDate }).IsUnique().HasDatabaseName("attendance_record_employee_id_attendance_date_key");
    builder.HasIndex(x => x.AttendanceDate).HasDatabaseName("idx_att_date");
    builder.HasIndex(x => x.WorkShiftId).HasDatabaseName("idx_attendance_record_work_shift_id");
  }
}

public class LeaveTypeConfiguration : EntityConfiguration<LeaveType, LeaveTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<LeaveType> builder)
  {
    base.Configure(builder);
    builder.ToTable("leave_type", t =>
      t.HasCheckConstraint("leave_type_max_days_per_year_check", "max_days_per_year IS NULL OR max_days_per_year >= 0"));

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.IsPaid).HasDefaultValue(true);
    builder.Property(x => x.MaxDaysPerYear).Numeric(6, 2);
    builder.Property(x => x.AccrualRule).Text();
    builder.Property(x => x.CarryForwardAllowed).HasDefaultValue(false);
    builder.Property(x => x.AffectsPayroll).HasDefaultValue(false);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("leave_type_name_key");
  }
}

public class LeaveEntitlementConfiguration : EntityConfiguration<LeaveEntitlement, LeaveEntitlementId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<LeaveEntitlement> builder)
  {
    base.Configure(builder);
    builder.ToTable("leave_entitlement", t =>
    {
      t.HasCheckConstraint("leave_entitlement_year_check", "year BETWEEN 2000 AND 2100");
      t.HasCheckConstraint("leave_entitlement_entitled_days_check", "entitled_days >= 0");
    });

    builder.Property(x => x.EntitledDays).Numeric(6, 2).HasDefaultValueSql("0");

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique()
      .HasDatabaseName("leave_entitlement_employee_id_leave_type_id_year_key");
    builder.HasIndex(x => x.LeaveTypeId).HasDatabaseName("idx_leave_entitlement_leave_type_id");
  }
}

public class LeaveApplicationConfiguration : EntityConfiguration<LeaveApplication, LeaveApplicationId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt | AuditColumns.UpdatedAt | AuditColumns.UpdatedBy;

  public override void Configure(EntityTypeBuilder<LeaveApplication> builder)
  {
    base.Configure(builder);
    builder.ToTable("leave_application", t =>
    {
      t.HasCheckConstraint("leave_application_days_check", "days > 0");
      t.HasCheckConstraint("ck_la_dates", "end_date >= start_date");
      t.HasCheckConstraint("ck_la_approval", "status <> 'approved' OR (approved_by IS NOT NULL AND approval_date IS NOT NULL)");
    });

    builder.Property(x => x.Days).Numeric(6, 2);
    builder.Property(x => x.Status).HasDefaultValue(LeaveStatus.Pending);
    builder.Property(x => x.AppliedDate).HasDefaultValueSql("CURRENT_DATE");
    builder.Property(x => x.Reason).Text();
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.StartDate }).HasDatabaseName("idx_la_employee_start");
    builder.HasIndex(x => x.Status).HasDatabaseName("idx_la_status");
    builder.HasIndex(x => x.LeaveTypeId).HasDatabaseName("idx_leave_application_leave_type_id");
  }
}

public class LeaveLedgerEntryConfiguration : EntityConfiguration<LeaveLedgerEntry, LeaveLedgerEntryId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<LeaveLedgerEntry> builder)
  {
    base.Configure(builder);
    builder.ToTable("leave_ledger", t =>
    {
      t.HasCheckConstraint("leave_ledger_days_check", "days <> 0");
      t.HasCheckConstraint("ck_ll_sign",
        "(txn_type IN ('opening','accrual','carry_forward','usage_reversal') AND days > 0) OR (txn_type IN ('usage','lapse','encashment') AND days < 0) OR txn_type = 'adjustment'");
      t.HasCheckConstraint("ck_ll_app", "txn_type NOT IN ('usage','usage_reversal') OR leave_application_id IS NOT NULL");
    });

    builder.Property(x => x.TransactionType).HasColumnName("txn_type");
    builder.Property(x => x.TransactionDate).HasColumnName("txn_date").HasDefaultValueSql("CURRENT_DATE");
    builder.Property(x => x.Days).Numeric(6, 2);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LeaveApplication>().WithMany().HasForeignKey(x => x.LeaveApplicationId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.LeaveApplicationId).HasDatabaseName("idx_leave_ledger_leave_application_id");
    builder.HasIndex(x => x.LeaveTypeId).HasDatabaseName("idx_leave_ledger_leave_type_id");
    builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).HasDatabaseName("idx_ll_emp_type_year");
  }
}
