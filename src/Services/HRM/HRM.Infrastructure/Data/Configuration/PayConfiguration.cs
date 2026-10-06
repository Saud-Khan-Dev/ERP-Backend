using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 11-13: salary structure, income tax, loans / advances and GP Fund. gp_fund_transaction is append-only and
// employee_tax_ledger is locked with its payroll run (triggers in HrmsSchemaSql).

public class SalaryComponentConfiguration : EntityConfiguration<SalaryComponent, SalaryComponentId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<SalaryComponent> builder)
  {
    base.Configure(builder);
    builder.ToTable("salary_component");

    builder.Property(x => x.ComponentCode).IsRequired();
    builder.Property(x => x.ComponentName).IsRequired();
    builder.Property(x => x.IsTaxable).HasDefaultValue(true);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.ComponentCode).IsUnique().HasDatabaseName("salary_component_component_code_key");
  }
}

public class SalaryComponentRuleConfiguration : EntityConfiguration<SalaryComponentRule, SalaryComponentRuleId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<SalaryComponentRule> builder)
  {
    base.Configure(builder);
    builder.ToTable("salary_component_rule", t =>
    {
      t.HasCheckConstraint("salary_component_rule_fixed_amount_check", "fixed_amount IS NULL OR fixed_amount >= 0");
      t.HasCheckConstraint("salary_component_rule_percentage_check", "percentage IS NULL OR percentage BETWEEN 0 AND 1000");
      t.HasCheckConstraint("salary_component_rule_min_bps_check", "min_bps IS NULL OR min_bps BETWEEN 1 AND 22");
      t.HasCheckConstraint("salary_component_rule_max_bps_check", "max_bps IS NULL OR max_bps BETWEEN 1 AND 22");
      t.HasCheckConstraint("ck_scr_dates", "effective_to IS NULL OR effective_to >= effective_from");
      t.HasCheckConstraint("ck_scr_bps", "min_bps IS NULL OR max_bps IS NULL OR min_bps <= max_bps");
      t.HasCheckConstraint("ck_scr_amount", "min_amount IS NULL OR max_amount IS NULL OR min_amount <= max_amount");
      t.HasCheckConstraint("ck_scr_method",
        "(calculation_method = 'fixed' AND fixed_amount IS NOT NULL) OR (calculation_method = 'percentage' AND percentage IS NOT NULL AND calculation_base IS NOT NULL) OR (calculation_method = 'formula' AND formula_expression IS NOT NULL) OR (calculation_method = 'tiered')");
    });

    builder.Property(x => x.RuleVersion).IsRequired();
    builder.Property(x => x.FixedAmount).Money();
    builder.Property(x => x.Percentage).Numeric(6, 3);
    builder.Property(x => x.FormulaExpression).Text();
    builder.Property(x => x.MinAmount).Money();
    builder.Property(x => x.MaxAmount).Money();
    builder.Property(x => x.Priority).HasDefaultValue(100);
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);

    builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(x => x.SalaryComponentId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Designation>().WithMany().HasForeignKey(x => x.ApplicableDesignationId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.ApplicableOrgUnitId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.SalaryComponentId, x.RuleVersion }).IsUnique()
      .HasDatabaseName("salary_component_rule_salary_component_id_rule_version_key");
    builder.HasIndex(x => x.ApplicableDesignationId).HasDatabaseName("idx_salary_component_rule_applicable_designation_id");
    builder.HasIndex(x => x.ApplicableOrgUnitId).HasDatabaseName("idx_salary_component_rule_applicable_org_unit_id");
    builder.HasIndex(x => new { x.SalaryComponentId, x.EffectiveFrom }).HasDatabaseName("idx_scr_component_from");
  }
}

public class EmployeeSalaryComponentConfiguration : EntityConfiguration<EmployeeSalaryComponent, SalaryOverrideId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeeSalaryComponent> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_salary_component", t =>
    {
      t.HasCheckConstraint("employee_salary_component_override_fixed_amount_check", "override_fixed_amount IS NULL OR override_fixed_amount >= 0");
      t.HasCheckConstraint("employee_salary_component_override_percentage_check", "override_percentage IS NULL OR override_percentage BETWEEN 0 AND 1000");
      t.HasCheckConstraint("ck_esc_dates", "effective_to IS NULL OR effective_to >= effective_from");
      t.HasCheckConstraint("ck_esc_one", "num_nonnulls(override_fixed_amount, override_percentage) = 1");
    });

    builder.Property(x => x.OverrideFixedAmount).Money();
    builder.Property(x => x.OverridePercentage).Numeric(6, 3);
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(x => x.SalaryComponentId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.PostId).HasDatabaseName("idx_employee_salary_component_post_id");
    builder.HasIndex(x => x.SalaryComponentId).HasDatabaseName("idx_employee_salary_component_salary_component_id");
  }
}

public class TaxYearConfiguration : EntityConfiguration<TaxYear, TaxYearId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<TaxYear> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_year", t => t.HasCheckConstraint("ck_ty_dates", "end_date > start_date"));

    builder.Property(x => x.YearLabel).IsRequired();
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);
    builder.HasIndex(x => x.YearLabel).IsUnique().HasDatabaseName("tax_year_year_label_key");

    builder.HasMany(x => x.Slabs).WithOne().HasForeignKey(s => s.TaxYearId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Slabs).HasField("_slabs").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class TaxSlabConfiguration : EntityConfiguration<TaxSlab, TaxSlabId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<TaxSlab> builder)
  {
    base.Configure(builder);
    builder.ToTable("tax_slab", t =>
    {
      t.HasCheckConstraint("tax_slab_min_income_check", "min_income >= 0");
      t.HasCheckConstraint("tax_slab_fixed_amount_check", "fixed_amount >= 0");
      t.HasCheckConstraint("tax_slab_rate_percentage_check", "rate_percentage BETWEEN 0 AND 100");
      t.HasCheckConstraint("ck_slab_range", "max_income IS NULL OR max_income > min_income");
    });

    builder.Property(x => x.MinIncome).Money();
    builder.Property(x => x.MaxIncome).Money();
    builder.Property(x => x.FixedAmount).Money().HasDefaultValueSql("0");
    builder.Property(x => x.RatePercentage).Numeric(6, 3).HasDefaultValueSql("0");
    builder.HasIndex(x => new { x.TaxYearId, x.SlabOrder }).IsUnique().HasDatabaseName("tax_slab_tax_year_id_slab_order_key");
  }
}

public class EmployeeTaxExemptionConfiguration : EntityConfiguration<EmployeeTaxExemption, TaxExemptionId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<EmployeeTaxExemption> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_tax_exemption", t => t.HasCheckConstraint("employee_tax_exemption_amount_check", "amount >= 0"));

    builder.Property(x => x.ExemptionType).IsRequired();
    builder.Property(x => x.Amount).Money();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<TaxYear>().WithMany().HasForeignKey(x => x.TaxYearId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.TaxYearId, x.ExemptionType }).IsUnique()
      .HasDatabaseName("employee_tax_exemption_employee_id_tax_year_id_exemption_ty_key");
    builder.HasIndex(x => x.TaxYearId).HasDatabaseName("idx_employee_tax_exemption_tax_year_id");
  }
}

public class EmployeeTaxLedgerEntryConfiguration : EntityConfiguration<EmployeeTaxLedgerEntry, TaxLedgerEntryId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeTaxLedgerEntry> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_tax_ledger", t =>
    {
      t.HasCheckConstraint("employee_tax_ledger_taxable_income_check", "taxable_income >= 0");
      t.HasCheckConstraint("employee_tax_ledger_tax_withheld_check", "tax_withheld >= 0");
    });

    builder.Property(x => x.TaxableIncome).Money();
    builder.Property(x => x.TaxWithheld).Money();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<TaxYear>().WithMany().HasForeignKey(x => x.TaxYearId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayrollTransaction>().WithMany().HasForeignKey(x => x.PayrollTransactionId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.PayrollTransactionId).IsUnique().HasDatabaseName("employee_tax_ledger_payroll_transaction_id_key");
    builder.HasIndex(x => x.TaxYearId).HasDatabaseName("idx_employee_tax_ledger_tax_year_id");
    builder.HasIndex(x => new { x.EmployeeId, x.TaxYearId }).HasDatabaseName("idx_etl_emp_year");
  }
}

public class LoanTypeConfiguration : EntityConfiguration<LoanType, LoanTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<LoanType> builder)
  {
    base.Configure(builder);
    builder.ToTable("loan_type", t => t.HasCheckConstraint("loan_type_default_interest_rate_check", "default_interest_rate >= 0"));

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.IsGpfAdvance).HasDefaultValue(false);
    builder.Property(x => x.DefaultInterestRate).Numeric(6, 3).HasDefaultValueSql("0");
    builder.Property(x => x.IsActive).HasDefaultValue(true);

    builder.HasOne<SalaryComponent>().WithMany().HasForeignKey(x => x.SalaryComponentId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("loan_type_name_key");
    builder.HasIndex(x => x.SalaryComponentId).HasDatabaseName("idx_loan_type_salary_component_id");
  }
}

public class EmployeeLoanConfiguration : EntityConfiguration<EmployeeLoan, EmployeeLoanId>
{
  public override void Configure(EntityTypeBuilder<EmployeeLoan> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_loan", t =>
    {
      t.HasCheckConstraint("employee_loan_principal_amount_check", "principal_amount > 0");
      t.HasCheckConstraint("employee_loan_interest_amount_check", "interest_amount >= 0");
      t.HasCheckConstraint("employee_loan_installments_count_check", "installments_count > 0");
      t.HasCheckConstraint("employee_loan_monthly_installment_check", "monthly_installment > 0");
      t.HasCheckConstraint("employee_loan_remaining_balance_check", "remaining_balance >= 0");
      t.HasCheckConstraint("ck_el_dates", "end_date IS NULL OR end_date >= start_date");
    });

    builder.Property(x => x.PrincipalAmount).Money();
    builder.Property(x => x.InterestAmount).Money().HasDefaultValueSql("0");
    builder.Property(x => x.MonthlyInstallment).Money();
    builder.Property(x => x.RemainingBalance).Money();
    builder.Property(x => x.DeductionPriority).HasDefaultValue(100);
    builder.Property(x => x.Status).HasDefaultValue(LoanStatus.Active);
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<LoanType>().WithMany().HasForeignKey(x => x.LoanTypeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.Status }).HasDatabaseName("idx_el_employee_status");
    builder.HasIndex(x => x.LoanTypeId).HasDatabaseName("idx_employee_loan_loan_type_id");

    builder.HasMany(x => x.Installments).WithOne().HasForeignKey(i => i.EmployeeLoanId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Installments).HasField("_installments").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class LoanInstallmentConfiguration : EntityConfiguration<LoanInstallment, LoanInstallmentId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<LoanInstallment> builder)
  {
    base.Configure(builder);
    builder.ToTable("loan_installment_schedule", t =>
    {
      t.HasCheckConstraint("loan_installment_schedule_installment_number_check", "installment_number > 0");
      t.HasCheckConstraint("loan_installment_schedule_amount_check", "amount > 0");
      t.HasCheckConstraint("loan_installment_schedule_paid_amount_check", "paid_amount >= 0");
      t.HasCheckConstraint("ck_lis_paid", "paid_amount <= amount");
    });

    builder.Property(x => x.Amount).Money();
    builder.Property(x => x.PaidAmount).Money().HasDefaultValueSql("0");
    builder.Property(x => x.Status).HasDefaultValue(InstallmentStatus.Pending);

    // target of payroll_loan_deduction's composite FK: the deducted installment belongs to the deducted loan
    builder.HasAlternateKey(x => new { x.Id, x.EmployeeLoanId }).HasName("uq_lis_id_loan");

    builder.HasIndex(x => new { x.EmployeeLoanId, x.InstallmentNumber }).IsUnique()
      .HasDatabaseName("loan_installment_schedule_employee_loan_id_installment_numb_key");
    builder.HasIndex(x => x.DueDate).HasFilter("status IN ('pending', 'partial')").HasDatabaseName("idx_lis_due");
  }
}

public class GpFundAccountConfiguration : EntityConfiguration<GpFundAccount, GpFundAccountId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<GpFundAccount> builder)
  {
    base.Configure(builder);
    builder.ToTable("gp_fund_account", t =>
    {
      t.HasCheckConstraint("gp_fund_account_monthly_subscription_check", "monthly_subscription >= 0");
      t.HasCheckConstraint("ck_gpa_dates", "closed_on IS NULL OR closed_on >= opened_on");
    });

    builder.Property(x => x.MonthlySubscription).Money().HasDefaultValueSql("0");
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.EmployeeId).IsUnique().HasDatabaseName("gp_fund_account_employee_id_key");
    builder.HasIndex(x => x.AccountNumber).IsUnique().HasDatabaseName("gp_fund_account_account_number_key");
  }
}

public class GpFundInterestRateConfiguration : EntityConfiguration<GpFundInterestRate, GpFundInterestRateId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<GpFundInterestRate> builder)
  {
    base.Configure(builder);
    builder.ToTable("gp_fund_interest_rate", t =>
    {
      t.HasCheckConstraint("gp_fund_interest_rate_rate_percent_check", "rate_percent >= 0");
      t.HasCheckConstraint("ck_gir_dates", "effective_to IS NULL OR effective_to >= effective_from");
    });

    builder.Property(x => x.FiscalYear).IsRequired();
    builder.Property(x => x.RatePercent).Numeric(6, 3);
  }
}

public class GpFundTransactionConfiguration : EntityConfiguration<GpFundTransaction, GpFundTransactionId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<GpFundTransaction> builder)
  {
    base.Configure(builder);
    builder.ToTable("gp_fund_transaction", t =>
    {
      t.HasCheckConstraint("gp_fund_transaction_amount_check", "amount <> 0");
      t.HasCheckConstraint("ck_gpt_sign",
        "(txn_type IN ('opening','subscription','interest','advance_recovery') AND amount > 0) OR (txn_type IN ('advance','withdrawal','final_payment') AND amount < 0) OR txn_type = 'adjustment'");
    });

    builder.Property(x => x.TransactionType).HasColumnName("txn_type");
    builder.Property(x => x.TransactionDate).HasColumnName("txn_date");
    builder.Property(x => x.Amount).Money();
    builder.Property(x => x.Remarks).Text();

    builder.HasOne<GpFundAccount>().WithMany().HasForeignKey(x => x.GpFundAccountId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<EmployeeLoan>().WithMany().HasForeignKey(x => x.EmployeeLoanId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayrollTransaction>().WithMany().HasForeignKey(x => x.PayrollTransactionId)
      .OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_gpt_payroll");

    builder.HasIndex(x => x.EmployeeLoanId).HasDatabaseName("idx_gp_fund_transaction_employee_loan_id");
    builder.HasIndex(x => x.PayrollTransactionId).HasDatabaseName("idx_gp_fund_transaction_payroll_transaction_id");
    builder.HasIndex(x => new { x.GpFundAccountId, x.TransactionDate }).HasDatabaseName("idx_gpt_account_date");
  }
}
