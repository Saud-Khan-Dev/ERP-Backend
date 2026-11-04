using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 7: employment history. employee_service_history is append-only (trigger); position_assignment capacity and
// "one regular post at a time" are enforced in the database too (HrmsSchemaSql).

public class ServiceEventTypeConfiguration : EntityConfiguration<ServiceEventType, ServiceEventTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<ServiceEventType> builder)
  {
    base.Configure(builder);
    builder.ToTable("service_event_type");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("service_event_type_name_key");
  }
}

public class EmployeeServiceHistoryConfiguration : EntityConfiguration<EmployeeServiceHistory, ServiceHistoryId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeeServiceHistory> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_service_history");

    builder.Property(x => x.Reason).Text();
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<ServiceEventType>().WithMany().HasForeignKey(x => x.EventTypeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.OldPostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.NewPostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.OldGradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.NewGradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.OldOrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.NewOrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<RecruitmentMethod>().WithMany().HasForeignKey(x => x.RecruitmentMethodId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.SupportingDocumentId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.NewGradeId).HasDatabaseName("idx_employee_service_history_new_grade_id");
    builder.HasIndex(x => x.NewOrgUnitId).HasDatabaseName("idx_employee_service_history_new_org_unit_id");
    builder.HasIndex(x => x.NewPostId).HasDatabaseName("idx_employee_service_history_new_post_id");
    builder.HasIndex(x => x.OldGradeId).HasDatabaseName("idx_employee_service_history_old_grade_id");
    builder.HasIndex(x => x.OldOrgUnitId).HasDatabaseName("idx_employee_service_history_old_org_unit_id");
    builder.HasIndex(x => x.OldPostId).HasDatabaseName("idx_employee_service_history_old_post_id");
    builder.HasIndex(x => x.RecruitmentMethodId).HasDatabaseName("idx_employee_service_history_recruitment_method_id");
    builder.HasIndex(x => x.SupportingDocumentId).HasDatabaseName("idx_employee_service_history_supporting_document_id");
    builder.HasIndex(x => new { x.EmployeeId, x.EffectiveDate }).HasDatabaseName("idx_esh_employee_date");
    builder.HasIndex(x => x.EventTypeId).HasDatabaseName("idx_esh_event");
  }
}

public class PositionAssignmentConfiguration : EntityConfiguration<PositionAssignment, PositionAssignmentId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PositionAssignment> builder)
  {
    base.Configure(builder);
    builder.ToTable("position_assignment", t =>
      t.HasCheckConstraint("ck_pa_dates", "effective_to IS NULL OR effective_to >= effective_from"));

    builder.Property(x => x.AssignmentType).HasDefaultValue(AssignmentType.Regular);
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeServiceHistory>().WithMany().HasForeignKey(x => x.ServiceHistoryId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.OrderDocumentId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_pa_employee");
    builder.HasIndex(x => new { x.PostId, x.EffectiveFrom }).HasDatabaseName("idx_pa_post_from");
    builder.HasIndex(x => x.OrderDocumentId).HasDatabaseName("idx_position_assignment_order_document_id");
    builder.HasIndex(x => x.ServiceHistoryId).HasDatabaseName("idx_position_assignment_service_history_id");
  }
}

public class HrActionRequestConfiguration : EntityConfiguration<HrActionRequest, HrActionRequestId>
{
  public override void Configure(EntityTypeBuilder<HrActionRequest> builder)
  {
    base.Configure(builder);
    builder.ToTable("hr_action_request", t =>
    {
      t.HasCheckConstraint("ck_har_applied", "status <> 'applied' OR resulting_service_history_id IS NOT NULL");
      t.HasCheckConstraint("ck_har_end", "end_date IS NULL OR end_date >= effective_date");
      t.HasCheckConstraint("ck_har_pay", "(stage_number IS NULL OR stage_number >= 0) AND (basic_pay IS NULL OR basic_pay >= 0) AND (notice_period_days IS NULL OR notice_period_days >= 0)");
    });

    builder.Property(x => x.Reason).Text();
    builder.Property(x => x.DecisionRemarks).Text();
    builder.Property(x => x.BasicPay).Money();
    builder.Property(x => x.Status).HasDefaultValue(HrActionStatus.Draft);
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.OldPostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.NewPostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.OldGradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.NewGradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.OldOrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.NewOrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.SupportingDocumentId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeServiceHistory>().WithMany().HasForeignKey(x => x.ResultingServiceHistoryId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<ServiceEventType>().WithMany().HasForeignKey(x => x.EventTypeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<RecruitmentMethod>().WithMany().HasForeignKey(x => x.RecruitmentMethodId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LeaveApplication>().WithMany().HasForeignKey(x => x.LeaveApplicationId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmploymentMethod>().WithMany().HasForeignKey(x => x.EmploymentMethodId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.EffectiveDate }).HasDatabaseName("idx_har_employee");
    builder.HasIndex(x => x.EventTypeId).HasDatabaseName("idx_hr_action_request_event_type_id");
    builder.HasIndex(x => x.RecruitmentMethodId).HasDatabaseName("idx_hr_action_request_recruitment_method_id");
    builder.HasIndex(x => x.LeaveApplicationId).HasDatabaseName("idx_hr_action_request_leave_application_id");
    builder.HasIndex(x => x.Status).HasDatabaseName("idx_har_status");
    builder.HasIndex(x => x.NewGradeId).HasDatabaseName("idx_hr_action_request_new_grade_id");
    builder.HasIndex(x => x.NewOrgUnitId).HasDatabaseName("idx_hr_action_request_new_org_unit_id");
    builder.HasIndex(x => x.NewPostId).HasDatabaseName("idx_hr_action_request_new_post_id");
    builder.HasIndex(x => x.OldGradeId).HasDatabaseName("idx_hr_action_request_old_grade_id");
    builder.HasIndex(x => x.OldOrgUnitId).HasDatabaseName("idx_hr_action_request_old_org_unit_id");
    builder.HasIndex(x => x.OldPostId).HasDatabaseName("idx_hr_action_request_old_post_id");
    builder.HasIndex(x => x.ResultingServiceHistoryId).HasDatabaseName("idx_hr_action_request_resulting_service_history_id");
    builder.HasIndex(x => x.SupportingDocumentId).HasDatabaseName("idx_hr_action_request_supporting_document_id");
  }
}

public class EmployeeSeparationConfiguration : EntityConfiguration<EmployeeSeparation, SeparationId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeeSeparation> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_separation", t =>
    {
      t.HasCheckConstraint("employee_separation_notice_period_days_check", "notice_period_days IS NULL OR notice_period_days >= 0");
      t.HasCheckConstraint("employee_separation_outstanding_loan_amount_check", "outstanding_loan_amount IS NULL OR outstanding_loan_amount >= 0");
      t.HasCheckConstraint("employee_separation_leave_encashment_amount_check", "leave_encashment_amount IS NULL OR leave_encashment_amount >= 0");
    });

    builder.Property(x => x.Reason).Text();
    builder.Property(x => x.FinalSettlementAmount).Money();
    builder.Property(x => x.OutstandingLoanAmount).Money();
    builder.Property(x => x.LeaveEncashmentAmount).Money();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeServiceHistory>().WithMany().HasForeignKey(x => x.ServiceHistoryId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayrollTransaction>().WithMany().HasForeignKey(x => x.SettlementPayrollTransactionId)
      .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_sep_settlement");

    builder.HasIndex(x => x.EmployeeId).IsUnique().HasDatabaseName("uq_separation_employee");
    builder.HasIndex(x => x.ServiceHistoryId).HasDatabaseName("idx_employee_separation_service_history_id");
    builder.HasIndex(x => x.SettlementPayrollTransactionId).HasDatabaseName("idx_employee_separation_settlement_payroll_transaction_id");
  }
}
