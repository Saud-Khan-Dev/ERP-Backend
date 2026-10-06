using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 14-17: payroll engine, payment, tasks and employee requests. A finalized run's rows are locked by triggers;
// the payslip snapshot is immutable; employee_task_update is append-only (HrmsSchemaSql).

public class PayrollPeriodConfiguration : EntityConfiguration<PayrollPeriod, PayrollPeriodId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayrollPeriod> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_period", t =>
    {
      t.HasCheckConstraint("payroll_period_year_check", "year BETWEEN 2000 AND 2100");
      t.HasCheckConstraint("payroll_period_month_check", "month BETWEEN 1 AND 12");
      t.HasCheckConstraint("ck_pperiod_dates", "end_date >= start_date");
    });

    builder.Property(x => x.Status).HasDefaultValue(PayrollPeriodStatus.Open);
    builder.HasIndex(x => new { x.Year, x.Month }).IsUnique().HasDatabaseName("payroll_period_year_month_key");
  }
}

public class PayrollRunConfiguration : EntityConfiguration<PayrollRun, PayrollRunId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt | AuditColumns.UpdatedAt;

  public override void Configure(EntityTypeBuilder<PayrollRun> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_run", t =>
    {
      t.HasCheckConstraint("ck_pr_approved", "status NOT IN ('approved','finalized','paid') OR approved_by IS NOT NULL");
      t.HasCheckConstraint("ck_pr_reverses", "reverses_run_id IS NULL OR reverses_run_id <> id");
    });

    builder.Property(x => x.RunType).HasDefaultValue(PayrollRunType.Regular);
    builder.Property(x => x.Status).HasDefaultValue(PayrollRunStatus.Draft);
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    builder.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.ReversesRunId).OnDelete(DeleteBehavior.NoAction);

    // exactly one live regular run per period; supplementary / arrears / bonus / final-settlement runs may repeat
    builder.HasIndex(x => x.PayrollPeriodId).IsUnique()
      .HasFilter("run_type = 'regular' AND status <> 'reversed'")
      .HasDatabaseName("uq_payroll_run_regular");
    builder.HasIndex(x => x.ReversesRunId).HasDatabaseName("idx_payroll_run_reverses_run_id");
    builder.HasIndex(x => x.Status).HasDatabaseName("idx_payroll_run_status");
  }
}

public class PayrollTransactionConfiguration : EntityConfiguration<PayrollTransaction, PayrollTransactionId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayrollTransaction> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_transaction", t =>
    {
      t.HasCheckConstraint("payroll_transaction_days_payable_check", "days_payable IS NULL OR days_payable BETWEEN 0 AND 31");
      t.HasCheckConstraint("payroll_transaction_gross_pay_check", "gross_pay >= 0");
      t.HasCheckConstraint("payroll_transaction_total_deductions_check", "total_deductions >= 0");
      t.HasCheckConstraint("ck_pt_net", "net_payable = gross_pay - total_deductions");
    });

    builder.Property(x => x.DaysPayable).Numeric(5, 2);
    builder.Property(x => x.GrossPay).Money().HasDefaultValueSql("0");
    builder.Property(x => x.TotalDeductions).Money().HasDefaultValueSql("0");
    builder.Property(x => x.NetPayable).Money().HasDefaultValueSql("0");
    builder.Property(x => x.Status).HasDefaultValue(PayrollTransactionStatus.Calculated);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<PayrollRun>().WithMany().HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique().HasDatabaseName("payroll_transaction_payroll_run_id_employee_id_key");
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_pt_employee");

    builder.HasMany(x => x.Segments).WithOne().HasForeignKey(s => s.PayrollTransactionId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Segments).HasField("_segments").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.PayrollTransactionId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.LoanDeductions).WithOne().HasForeignKey(d => d.PayrollTransactionId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.LoanDeductions).HasField("_loanDeductions").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.Adjustments).WithOne().HasForeignKey(a => a.PayrollTransactionId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Adjustments).HasField("_adjustments").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PayrollTransactionSegmentConfiguration : EntityConfiguration<PayrollTransactionSegment, PayrollSegmentId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayrollTransactionSegment> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_transaction_segment", t =>
    {
      t.HasCheckConstraint("payroll_transaction_segment_basic_pay_check", "basic_pay >= 0");
      t.HasCheckConstraint("payroll_transaction_segment_days_check", "days > 0 AND days <= 31");
      t.HasCheckConstraint("ck_pts_dates", "period_to >= period_from");
    });

    builder.Property(x => x.BasicPay).Money();
    builder.Property(x => x.Days).Numeric(5, 2);

    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleStage>().WithMany().HasForeignKey(x => x.PayScaleStageId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.GradeId).HasDatabaseName("idx_payroll_transaction_segment_grade_id");
    builder.HasIndex(x => x.PayScaleStageId).HasDatabaseName("idx_payroll_transaction_segment_pay_scale_stage_id");
    builder.HasIndex(x => x.PostId).HasDatabaseName("idx_payroll_transaction_segment_post_id");
  }
}

public class PayrollLoanDeductionConfiguration : EntityConfiguration<PayrollLoanDeduction, PayrollLoanDeductionId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayrollLoanDeduction> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_loan_deduction", t =>
      t.HasCheckConstraint("payroll_loan_deduction_installment_amount_check", "installment_amount > 0"));

    builder.Property(x => x.InstallmentAmount).Money();

    builder.HasOne<EmployeeLoan>().WithMany().HasForeignKey(x => x.EmployeeLoanId).OnDelete(DeleteBehavior.NoAction);
    // the installment must belong to the deducted loan
    builder.HasOne<LoanInstallment>().WithMany()
      .HasForeignKey(x => new { x.InstallmentId, x.EmployeeLoanId })
      .HasPrincipalKey(i => new { i.Id, i.EmployeeLoanId })
      .OnDelete(DeleteBehavior.NoAction)
      .HasConstraintName("fk_pld_installment");

    // an installment is deducted at most once
    builder.HasIndex(x => x.InstallmentId).IsUnique().HasDatabaseName("payroll_loan_deduction_installment_id_key");
    builder.HasIndex(x => x.EmployeeLoanId).HasDatabaseName("idx_payroll_loan_deduction_employee_loan_id");
    builder.HasIndex(x => new { x.InstallmentId, x.EmployeeLoanId }).HasDatabaseName("idx_payroll_loan_deduction_installment_id_employee_loan_id");
    builder.HasIndex(x => x.PayrollTransactionId).HasDatabaseName("idx_payroll_loan_deduction_payroll_transaction_id");
  }
}

public class PayrollComponentDetailConfiguration : EntityConfiguration<PayrollComponentDetail, PayrollComponentLineId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayrollComponentDetail> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_component_detail", t =>
    {
      t.HasCheckConstraint("payroll_component_detail_calculated_amount_check", "calculated_amount >= 0");
      t.HasCheckConstraint("ck_pcd_loan", "(source = 'loan') = (payroll_loan_deduction_id IS NOT NULL)");
      t.HasCheckConstraint("ck_pcd_loan_type", "source <> 'loan' OR component_type = 'deduction'");
    });

    builder.Property(x => x.Source).HasDefaultValue(ComponentSource.Rule);
    builder.Property(x => x.BaseAmount).Money();
    builder.Property(x => x.Rate).Numeric(6, 3);
    builder.Property(x => x.FormulaReference).Text();
    builder.Property(x => x.CalculatedAmount).Money();

    builder.HasOne<PayrollTransactionSegment>().WithMany().HasForeignKey(x => x.SegmentId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(x => x.SalaryComponentId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<SalaryComponentRule>().WithMany().HasForeignKey(x => x.SalaryComponentRuleId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayrollLoanDeduction>().WithMany().HasForeignKey(x => x.PayrollLoanDeductionId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.PayrollLoanDeductionId).IsUnique().HasDatabaseName("payroll_component_detail_payroll_loan_deduction_id_key");
    builder.HasIndex(x => x.SalaryComponentId).HasDatabaseName("idx_payroll_component_detail_salary_component_id");
    builder.HasIndex(x => x.SalaryComponentRuleId).HasDatabaseName("idx_payroll_component_detail_salary_component_rule_id");
    builder.HasIndex(x => x.SegmentId).HasDatabaseName("idx_payroll_component_detail_segment_id");
    builder.HasIndex(x => x.PayrollTransactionId).HasDatabaseName("idx_pcd_txn");
  }
}

public class PayrollAdjustmentConfiguration : EntityConfiguration<PayrollAdjustment, PayrollAdjustmentId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_adjustment", t => t.HasCheckConstraint("payroll_adjustment_amount_check", "amount <> 0"));

    builder.Property(x => x.Amount).Money();
    builder.Property(x => x.Reason).Text();
    builder.HasIndex(x => x.PayrollTransactionId).HasDatabaseName("idx_payroll_adjustment_payroll_transaction_id");
  }
}

public class PayslipConfiguration : EntityConfiguration<Payslip, PayslipId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<Payslip> builder)
  {
    base.Configure(builder);
    builder.ToTable("payslip");

    builder.Property(x => x.SnapshotJson).Jsonb().IsRequired();
    builder.Property(x => x.GeneratedAt).HasDefaultValueSql("now()");

    builder.HasOne<PayrollTransaction>().WithMany().HasForeignKey(x => x.PayrollTransactionId).OnDelete(DeleteBehavior.NoAction);
    builder.HasIndex(x => x.PayrollTransactionId).IsUnique().HasDatabaseName("payslip_payroll_transaction_id_key");
  }
}

public class PayrollPaymentConfiguration : EntityConfiguration<PayrollPayment, PayrollPaymentId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<PayrollPayment> builder)
  {
    base.Configure(builder);
    builder.ToTable("payroll_payment", t =>
      t.HasCheckConstraint("ck_pp_processed", "payment_status <> 'processed' OR (payment_date IS NOT NULL AND payment_reference IS NOT NULL)"));

    builder.Property(x => x.PaymentMethod).HasDefaultValue(PaymentMethod.BankTransfer);
    builder.Property(x => x.PaymentStatus).HasDefaultValue(PaymentStatus.Pending);

    builder.HasOne<PayrollTransaction>().WithMany().HasForeignKey(x => x.PayrollTransactionId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    // the account must belong to the same employee as the payment
    builder.HasOne<EmployeeBankAccount>().WithMany()
      .HasForeignKey(x => new { x.BankAccountId, x.EmployeeId })
      .HasPrincipalKey(a => new { a.Id, a.EmployeeId })
      .OnDelete(DeleteBehavior.NoAction)
      .HasConstraintName("fk_pp_bank_owner");

    builder.HasIndex(x => x.PayrollTransactionId).IsUnique()
      .HasFilter("payment_status IN ('pending', 'processed')")
      .HasDatabaseName("uq_pp_one_live");
    builder.HasIndex(x => x.BankAccountId).HasDatabaseName("idx_pp_bank");
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_payroll_payment_employee_id");
    builder.HasIndex(x => new { x.BankAccountId, x.EmployeeId }).HasDatabaseName("idx_payroll_payment_bank_account_id_employee_id");
  }
}

public class EmployeeTaskConfiguration : EntityConfiguration<EmployeeTask, EmployeeTaskId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt | AuditColumns.UpdatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeTask> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_task", t =>
    {
      t.HasCheckConstraint("employee_task_progress_percentage_check", "progress_percentage BETWEEN 0 AND 100");
      t.HasCheckConstraint("ck_et_completed", "(status = 'completed') = (completed_at IS NOT NULL)");
    });

    builder.Property(x => x.Title).IsRequired();
    builder.Property(x => x.Description).Text();
    builder.Property(x => x.Priority).HasDefaultValue(TaskPriority.Medium);
    builder.Property(x => x.Status).HasDefaultValue(EmployeeTaskStatus.Pending);
    builder.Property(x => x.ProgressPercentage).HasDefaultValue(0);

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.DueDate).HasDatabaseName("idx_et_due");
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_et_employee");
    builder.HasIndex(x => x.Status).HasDatabaseName("idx_et_status");

    builder.HasMany(x => x.Updates).WithOne().HasForeignKey(u => u.TaskId).OnDelete(DeleteBehavior.NoAction);
    builder.Navigation(x => x.Updates).HasField("_updates").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class EmployeeTaskUpdateConfiguration : EntityConfiguration<EmployeeTaskUpdate, TaskUpdateId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeTaskUpdate> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_task_update", t =>
      t.HasCheckConstraint("employee_task_update_progress_percentage_check", "progress_percentage BETWEEN 0 AND 100"));

    builder.Property(x => x.Notes).Text();
    builder.Property(x => x.AuthorId).HasColumnName("updated_by");
    builder.HasIndex(x => new { x.TaskId, x.CreatedAt }).HasDatabaseName("idx_etu_task");
  }
}

public class EmployeeRequestTypeConfiguration : EntityConfiguration<EmployeeRequestType, EmployeeRequestTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<EmployeeRequestType> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_request_type");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.RequiresDocument).HasDefaultValue(false);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("employee_request_type_name_key");
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("employee_request_type_code_key");
  }
}

public class EmployeeRequestConfiguration : EntityConfiguration<EmployeeRequest, EmployeeRequestId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<EmployeeRequest> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_request", t =>
      t.HasCheckConstraint("ck_er_review", "status NOT IN ('approved','rejected') OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL)"));

    builder.Property(x => x.Description).Text();
    builder.Property(x => x.RequestedData).Jsonb();
    builder.Property(x => x.Status).HasDefaultValue(EmployeeRequestStatus.Pending);
    builder.Property(x => x.SubmittedAt).HasDefaultValueSql("now()");
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeRequestType>().WithMany().HasForeignKey(x => x.RequestTypeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.SupportingDocumentId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.SupportingDocumentId).HasDatabaseName("idx_employee_request_supporting_document_id");
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_er_employee");
    builder.HasIndex(x => x.Status).HasDatabaseName("idx_er_status");
    builder.HasIndex(x => x.RequestTypeId).HasDatabaseName("idx_er_type");
  }
}

// ---- views (keyless, read-only) ----

public class HrmsViewConfiguration :
  IEntityTypeConfiguration<OrganizationUnitCurrent>,
  IEntityTypeConfiguration<PostOccupancy>,
  IEntityTypeConfiguration<LeaveBalance>,
  IEntityTypeConfiguration<GpFundBalance>,
  IEntityTypeConfiguration<EmployeeTaxYtd>
{
  public void Configure(EntityTypeBuilder<OrganizationUnitCurrent> builder) => builder.HasNoKey().ToView("v_organization_unit_current");
  public void Configure(EntityTypeBuilder<PostOccupancy> builder) => builder.HasNoKey().ToView("v_post_occupancy");
  public void Configure(EntityTypeBuilder<LeaveBalance> builder) => builder.HasNoKey().ToView("v_leave_balance");
  public void Configure(EntityTypeBuilder<GpFundBalance> builder) => builder.HasNoKey().ToView("v_gp_fund_balance");
  public void Configure(EntityTypeBuilder<EmployeeTaxYtd> builder) => builder.HasNoKey().ToView("v_employee_tax_ytd");
}
