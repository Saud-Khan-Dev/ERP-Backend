using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 4-6: employee master, documents & education, recruitment.
// The "one primary contact / account / highest qualification" rules are deferrable exclusion constraints created by
// HrmsSchemaSql (so moving the flag from one row to another in one save always succeeds).

public class EmployeeConfiguration : EntityConfiguration<Employee, EmployeeId>
{
  public override void Configure(EntityTypeBuilder<Employee> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee", t =>
    {
      t.HasCheckConstraint("ck_emp_method",
        "(employment_type = 'regular' AND employment_method IS NOT NULL) OR (employment_type <> 'regular' AND employment_method IS NULL)");
      t.HasCheckConstraint("ck_emp_cnic", "cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'");
      t.HasCheckConstraint("ck_emp_dob", "date_of_birth IS NULL OR date_of_birth < current_date");
      t.HasCheckConstraint("ck_emp_blood", "blood_group IS NULL OR blood_group IN ('A+','A-','B+','B-','AB+','AB-','O+','O-')");
    });

    builder.Property(x => x.EmployeeNumber).IsRequired();
    builder.Property(x => x.FirstName).IsRequired();
    builder.Property(x => x.FullName)
      .HasComputedColumnSql("first_name || coalesce(' ' || middle_name, '') || coalesce(' ' || last_name, '')", stored: true);
    builder.Property(x => x.Cnic).IsRequired();
    builder.Property(x => x.Nationality).HasDefaultValue(Employee.DefaultNationality);
    builder.Property(x => x.ProfileStatus).HasDefaultValue(RecordStatus.Active);
    builder.Property(x => x.EmploymentStatus).HasDefaultValue(EmploymentStatus.Active);
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    builder.HasIndex(x => x.EmployeeNumber).IsUnique().HasDatabaseName("employee_employee_number_key");
    builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("employee_user_id_key");
    builder.HasIndex(x => x.Cnic).IsUnique().HasDatabaseName("employee_cnic_key");
    builder.HasIndex(x => x.EmploymentStatus).HasDatabaseName("idx_employee_status");
    builder.HasIndex(x => x.EmploymentType).HasDatabaseName("idx_employee_type");

    builder.HasMany(x => x.Contacts).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Contacts).HasField("_contacts").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.Addresses).WithOne().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Addresses).HasField("_addresses").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.EmergencyContacts).WithOne().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.EmergencyContacts).HasField("_emergencyContacts").UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.HasMany(x => x.FamilyMembers).WithOne().HasForeignKey(m => m.EmployeeId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.FamilyMembers).HasField("_familyMembers").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class EmployeeContactConfiguration : EntityConfiguration<EmployeeContact, EmployeeContactId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeContact> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_contact");

    builder.Property(x => x.Value).IsRequired();
    builder.Property(x => x.IsPrimary).HasDefaultValue(false);
  }
}

public class EmployeeAddressConfiguration : EntityConfiguration<EmployeeAddress, EmployeeAddressId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeAddress> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_address");

    builder.HasIndex(x => new { x.EmployeeId, x.AddressType }).IsUnique()
      .HasFilter("address_type IN ('permanent', 'current')")
      .HasDatabaseName("uq_employee_address_type");
  }
}

public class EmployeeEmergencyContactConfiguration : EntityConfiguration<EmployeeEmergencyContact, EmergencyContactId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeEmergencyContact> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_emergency_contact");

    builder.Property(x => x.Name).IsRequired();
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_employee_emergency_contact_employee_id");
  }
}

public class EmployeeFamilyMemberConfiguration : EntityConfiguration<EmployeeFamilyMember, FamilyMemberId>
{
  protected override AuditColumns Audit => AuditColumns.CreatedAt;

  public override void Configure(EntityTypeBuilder<EmployeeFamilyMember> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_family_member", t =>
      t.HasCheckConstraint("employee_family_member_cnic_check", "cnic IS NULL OR cnic ~ '^[0-9]{5}-?[0-9]{7}-?[0-9]$'"));

    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_employee_family_member_employee_id");
  }
}

public class EmployeeBankAccountConfiguration : EntityConfiguration<EmployeeBankAccount, BankAccountId>
{
  public override void Configure(EntityTypeBuilder<EmployeeBankAccount> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_bank_account", t =>
    {
      t.HasCheckConstraint("ck_eba_iban", "iban IS NULL OR iban ~ '^PK[0-9]{2}[A-Z]{4}[0-9]{16}$'");
      t.HasCheckConstraint("ck_eba_ident", "account_number IS NOT NULL OR iban IS NOT NULL");
    });

    builder.Property(x => x.BankName).IsRequired();
    builder.Property(x => x.IsPrimary).HasDefaultValue(true);
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);

    // lets payroll_payment prove the account belongs to the paid employee (composite FK)
    builder.HasAlternateKey(x => new { x.Id, x.EmployeeId }).HasName("employee_bank_account_id_employee_id_key");

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
  }
}

public class EmployeePayRecordConfiguration : EntityConfiguration<EmployeePayRecord, EmployeePayRecordId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeePayRecord> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_pay_record", t =>
    {
      t.HasCheckConstraint("employee_pay_record_basic_pay_check", "basic_pay >= 0");
      t.HasCheckConstraint("ck_epr_dates", "effective_to IS NULL OR effective_to >= effective_from");
    });

    builder.Property(x => x.BasicPay).Money();

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleStage>().WithMany().HasForeignKey(x => x.PayScaleStageId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.PayScaleStageId).HasDatabaseName("idx_employee_pay_record_pay_scale_stage_id");
  }
}

public class DocumentTypeConfiguration : EntityConfiguration<DocumentType, DocumentTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<DocumentType> builder)
  {
    base.Configure(builder);
    builder.ToTable("document_type");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.RequiresExpiry).HasDefaultValue(false);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("document_type_name_key");
  }
}

public class EmployeeDocumentConfiguration : EntityConfiguration<EmployeeDocument, EmployeeDocumentId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeeDocument> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_document", t =>
    {
      t.HasCheckConstraint("ck_ed_dates", "expiry_date IS NULL OR issue_date IS NULL OR expiry_date >= issue_date");
      t.HasCheckConstraint("ck_ed_verified", "verification_status <> 'verified' OR (verified_by IS NOT NULL AND verification_date IS NOT NULL)");
    });

    builder.Property(x => x.FileReference).IsRequired();
    builder.Property(x => x.VerificationStatus).HasDefaultValue(VerificationStatus.Unverified);

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.DocumentTypeId }).HasDatabaseName("idx_ed_employee");
    builder.HasIndex(x => x.ExpiryDate).HasFilter("expiry_date IS NOT NULL").HasDatabaseName("idx_ed_expiry");
    builder.HasIndex(x => x.DocumentTypeId).HasDatabaseName("idx_employee_document_document_type_id");
  }
}

public class EmployeeEducationConfiguration : EntityConfiguration<EmployeeEducation, EducationId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<EmployeeEducation> builder)
  {
    base.Configure(builder);
    builder.ToTable("employee_education", t =>
      t.HasCheckConstraint("employee_education_passing_year_check", "passing_year IS NULL OR passing_year BETWEEN 1950 AND 2100"));

    builder.Property(x => x.DegreeTitle).IsRequired();
    builder.Property(x => x.InstitutionName).IsRequired();
    builder.Property(x => x.IsHighestQualification).HasDefaultValue(false);

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => new { x.EmployeeId, x.QualificationLevel }).HasDatabaseName("idx_edu_employee_level");
  }
}

public class RecruitmentMethodConfiguration : EntityConfiguration<RecruitmentMethod, RecruitmentMethodId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<RecruitmentMethod> builder)
  {
    base.Configure(builder);
    builder.ToTable("recruitment_method");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("recruitment_method_name_key");
  }
}
