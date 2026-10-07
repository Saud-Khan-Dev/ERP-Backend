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
      t.HasCheckConstraint("ck_har_applied", "status <> 'applied' OR resulting_service_history_id IS NOT NULL"));

    builder.Property(x => x.Reason).Text();
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

    builder.HasIndex(x => new { x.EmployeeId, x.EffectiveDate }).HasDatabaseName("idx_har_employee");
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

public class PerformancePeriodConfiguration : EntityConfiguration<PerformancePeriod, PerformancePeriodId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PerformancePeriod> builder)
  {
    base.Configure(builder);
    builder.ToTable("performance_period", t => t.HasCheckConstraint("ck_pp_dates", "end_date >= start_date"));

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);
  }
}

public class PerformanceReviewConfiguration : EntityConfiguration<PerformanceReview, PerformanceReviewId>
{
  public override void Configure(EntityTypeBuilder<PerformanceReview> builder)
  {
    base.Configure(builder);
    builder.ToTable("performance_review", t =>
    {
      t.HasCheckConstraint("performance_review_final_score_check", "final_score IS NULL OR final_score BETWEEN 0 AND 100");
      t.HasCheckConstraint("ck_pr_not_self", "employee_id <> evaluator_id");
    });

    builder.Property(x => x.Status).HasDefaultValue(ReviewStatus.Draft);
    builder.Property(x => x.FinalScore).Numeric(6, 2);
    builder.Property(x => x.PromotionRecommended).HasDefaultValue(false);
    builder.Property(x => x.TrainingRecommended).HasDefaultValue(false);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PerformancePeriod>().WithMany().HasForeignKey(x => x.PerformancePeriodId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EvaluatorId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.PerformancePeriodId }).IsUnique()
      .HasDatabaseName("performance_review_employee_id_performance_period_id_key");
    builder.HasIndex(x => x.PerformancePeriodId).HasDatabaseName("idx_performance_review_performance_period_id");
    builder.HasIndex(x => x.EvaluatorId).HasDatabaseName("idx_pr_evaluator");

    builder.HasMany(x => x.Goals).WithOne().HasForeignKey(g => g.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Goals).HasField("_goals").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.Kpis).WithOne().HasForeignKey(k => k.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Kpis).HasField("_kpis").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.Competencies).WithOne().HasForeignKey(c => c.PerformanceReviewId).OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(x => x.Competencies).HasField("_competencies").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PerformanceGoalConfiguration : EntityConfiguration<PerformanceGoal, PerformanceGoalId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PerformanceGoal> builder)
  {
    base.Configure(builder);
    builder.ToTable("performance_goal", t =>
    {
      t.HasCheckConstraint("performance_goal_weight_check", "weight BETWEEN 0 AND 100");
      t.HasCheckConstraint("performance_goal_score_check", "score IS NULL OR score BETWEEN 0 AND 100");
    });

    builder.Property(x => x.Description).Text().IsRequired();
    builder.Property(x => x.Weight).Numeric(5, 2).HasDefaultValueSql("0");
    builder.Property(x => x.Target).Text();
    builder.Property(x => x.Achievement).Text();
    builder.Property(x => x.Score).Numeric(6, 2);
    builder.Property(x => x.SortOrder).HasDefaultValue(0);
    builder.HasIndex(x => x.PerformanceReviewId).HasDatabaseName("idx_performance_goal_performance_review_id");
  }
}

public class PerformanceKpiConfiguration : EntityConfiguration<PerformanceKpi, PerformanceKpiId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PerformanceKpi> builder)
  {
    base.Configure(builder);
    builder.ToTable("performance_kpi", t =>
      t.HasCheckConstraint("performance_kpi_score_check", "score IS NULL OR score BETWEEN 0 AND 100"));

    builder.Property(x => x.KpiName).IsRequired();
    builder.Property(x => x.Score).Numeric(6, 2);
    builder.Property(x => x.SortOrder).HasDefaultValue(0);
    builder.HasIndex(x => x.PerformanceReviewId).HasDatabaseName("idx_performance_kpi_performance_review_id");
  }
}

public class PerformanceCompetencyConfiguration : EntityConfiguration<PerformanceCompetency, PerformanceCompetencyId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PerformanceCompetency> builder)
  {
    base.Configure(builder);
    builder.ToTable("performance_competency", t =>
      t.HasCheckConstraint("performance_competency_rating_check", "rating IS NULL OR rating BETWEEN 0 AND 10"));

    builder.Property(x => x.CompetencyName).IsRequired();
    builder.Property(x => x.Rating).Numeric(4, 2);
    builder.Property(x => x.Remarks).Text();
    builder.Property(x => x.SortOrder).HasDefaultValue(0);
    builder.HasIndex(x => x.PerformanceReviewId).HasDatabaseName("idx_performance_competency_performance_review_id");
  }
}
