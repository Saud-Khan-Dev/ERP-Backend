using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// MODULE 1-3: organization hierarchy, designation & pay scale, sanctioned posts.
// EXCLUDE (no overlapping periods) constraints are created by HrmsSchemaSql.

public class OrganizationUnitTypeConfiguration : EntityConfiguration<OrganizationUnitType, OrganizationUnitTypeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<OrganizationUnitType> builder)
  {
    base.Configure(builder);
    builder.ToTable("organization_unit_type", t =>
      t.HasCheckConstraint("organization_unit_type_hierarchy_level_check", "hierarchy_level IS NULL OR hierarchy_level >= 0"));

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.Code).IsRequired();
    builder.Property(x => x.IsActive).HasDefaultValue(true);

    builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("organization_unit_type_name_key");
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("organization_unit_type_code_key");
  }
}

public class LocationConfiguration : EntityConfiguration<Location, LocationId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<Location> builder)
  {
    base.Configure(builder);
    builder.ToTable("location");

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.Province).HasDefaultValue(Location.DefaultProvince);
    builder.Property(x => x.IsActive).HasDefaultValue(true);
  }
}

public class OrganizationUnitConfiguration : EntityConfiguration<OrganizationUnit, OrganizationUnitId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<OrganizationUnit> builder)
  {
    base.Configure(builder);
    builder.ToTable("organization_unit");

    builder.Property(x => x.Code).IsRequired();
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("organization_unit_code_key");

    builder.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.OrgUnitId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class OrganizationUnitVersionConfiguration : EntityConfiguration<OrganizationUnitVersion, OrganizationUnitVersionId>
{
  public override void Configure(EntityTypeBuilder<OrganizationUnitVersion> builder)
  {
    base.Configure(builder);
    builder.ToTable("organization_unit_version", t =>
    {
      t.HasCheckConstraint("ck_ouv_dates", "effective_to IS NULL OR effective_to >= effective_from");
      t.HasCheckConstraint("ck_ouv_no_self", "parent_unit_id IS NULL OR parent_unit_id <> org_unit_id");
    });

    builder.Property(x => x.Name).IsRequired();
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);

    builder.HasOne<OrganizationUnitType>().WithMany().HasForeignKey(x => x.UnitTypeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.ParentUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.HeadPostId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("fk_ouv_head_post");

    builder.HasIndex(x => x.HeadPostId).HasDatabaseName("idx_organization_unit_version_head_post_id");
    builder.HasIndex(x => x.LocationId).HasDatabaseName("idx_organization_unit_version_location_id");
    builder.HasIndex(x => x.UnitTypeId).HasDatabaseName("idx_organization_unit_version_unit_type_id");
    builder.HasIndex(x => x.ParentUnitId).HasDatabaseName("idx_ouv_parent");
  }
}

public class DesignationConfiguration : EntityConfiguration<Designation, DesignationId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<Designation> builder)
  {
    base.Configure(builder);
    builder.ToTable("designation");

    builder.Property(x => x.Title).IsRequired();
    builder.Property(x => x.Description).Text();
    builder.Property(x => x.IsActive).HasDefaultValue(true);

    builder.HasIndex(x => x.Title).IsUnique().HasDatabaseName("designation_title_key");
    builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("designation_code_key");
  }
}

public class PayScaleGradeConfiguration : EntityConfiguration<PayScaleGrade, PayScaleGradeId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayScaleGrade> builder)
  {
    base.Configure(builder);
    builder.ToTable("pay_scale_grade", t =>
      t.HasCheckConstraint("pay_scale_grade_bps_number_check", "bps_number >= 1 AND bps_number <= 22"));

    builder.Property(x => x.IsActive).HasDefaultValue(true);
    builder.HasIndex(x => x.BpsNumber).IsUnique().HasDatabaseName("pay_scale_grade_bps_number_key");
  }
}

public class PayScaleVersionConfiguration : EntityConfiguration<PayScaleVersion, PayScaleVersionId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PayScaleVersion> builder)
  {
    base.Configure(builder);
    builder.ToTable("pay_scale_version", t =>
    {
      t.HasCheckConstraint("pay_scale_version_min_basic_pay_check", "min_basic_pay >= 0");
      t.HasCheckConstraint("ck_psv_pay", "max_basic_pay >= min_basic_pay");
      t.HasCheckConstraint("ck_psv_dates", "effective_to IS NULL OR effective_to >= effective_from");
    });

    builder.Property(x => x.MinBasicPay).Money();
    builder.Property(x => x.MaxBasicPay).Money();
    builder.Property(x => x.IncrementRule).Text();
    builder.Property(x => x.Status).HasDefaultValue(RecordStatus.Active);

    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.NoAction);

    builder.HasMany(x => x.Stages).WithOne().HasForeignKey(s => s.PayScaleVersionId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Stages).HasField("_stages").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PayScaleStageConfiguration : EntityConfiguration<PayScaleStage, PayScaleStageId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<PayScaleStage> builder)
  {
    base.Configure(builder);
    builder.ToTable("pay_scale_stage", t =>
    {
      t.HasCheckConstraint("pay_scale_stage_stage_number_check", "stage_number >= 0");
      t.HasCheckConstraint("pay_scale_stage_basic_pay_check", "basic_pay >= 0");
    });

    builder.Property(x => x.BasicPay).Money();
    builder.HasIndex(x => new { x.PayScaleVersionId, x.StageNumber }).IsUnique()
      .HasDatabaseName("pay_scale_stage_pay_scale_version_id_stage_number_key");
  }
}

public class PostConfiguration : EntityConfiguration<Post, PostId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<Post> builder)
  {
    base.Configure(builder);
    builder.ToTable("post");

    builder.Property(x => x.PostCode).IsRequired();
    builder.HasIndex(x => x.PostCode).IsUnique().HasDatabaseName("post_post_code_key");

    builder.HasMany(x => x.Versions).WithOne().HasForeignKey(v => v.PostId).OnDelete(DeleteBehavior.ClientCascade);
    builder.Navigation(x => x.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}

public class PostVersionConfiguration : EntityConfiguration<PostVersion, PostVersionId>
{
  protected override AuditColumns Audit => AuditColumns.Created;

  public override void Configure(EntityTypeBuilder<PostVersion> builder)
  {
    base.Configure(builder);
    builder.ToTable("post_version", t =>
    {
      t.HasCheckConstraint("post_version_sanctioned_count_check", "sanctioned_count >= 1");
      t.HasCheckConstraint("ck_pv_dates", "effective_to IS NULL OR effective_to >= effective_from");
      t.HasCheckConstraint("ck_pv_self", "reporting_post_id IS NULL OR reporting_post_id <> post_id");
    });

    builder.Property(x => x.EmploymentType).HasDefaultValue(EmploymentType.Regular);
    builder.Property(x => x.SanctionedCount).HasDefaultValue(1);
    builder.Property(x => x.LifecycleStatus).HasDefaultValue(PostLifecycle.Sanctioned);

    builder.HasOne<Designation>().WithMany().HasForeignKey(x => x.DesignationId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<PayScaleGrade>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Post>().WithMany().HasForeignKey(x => x.ReportingPostId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);

    builder.HasIndex(x => x.DesignationId).HasDatabaseName("idx_post_version_designation_id");
    builder.HasIndex(x => x.GradeId).HasDatabaseName("idx_post_version_grade_id");
    builder.HasIndex(x => x.LocationId).HasDatabaseName("idx_post_version_location_id");
    builder.HasIndex(x => x.OrgUnitId).HasDatabaseName("idx_pv_org_unit");
    builder.HasIndex(x => x.ReportingPostId).HasDatabaseName("idx_pv_reporting");
  }
}
