using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AnnouncementConfiguration : EntityConfiguration<Announcement, AnnouncementId>
{
  public override void Configure(EntityTypeBuilder<Announcement> builder)
  {
    base.Configure(builder);
    builder.ToTable("announcement", t =>
    {
      t.HasCheckConstraint("ck_ann_dates", "show_until IS NULL OR show_until >= show_from");
      t.HasCheckConstraint("ck_ann_withdrawn", "NOT is_withdrawn OR (withdrawn_at IS NOT NULL AND withdrawn_by IS NOT NULL AND withdrawn_reason IS NOT NULL)");
      t.HasCheckConstraint("ck_ann_file", "(file_path IS NULL) = (file_name IS NULL)");
    });

    builder.Property(x => x.Title).IsRequired();
    builder.Property(x => x.Body).IsRequired().Text();
    builder.Property(x => x.IsPinned).HasDefaultValue(false);
    builder.Property(x => x.IsWithdrawn).HasDefaultValue(false);

    builder.HasOne<OrganizationUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.NoAction);
    builder.HasIndex(x => x.OrgUnitId).HasDatabaseName("idx_ann_org_unit");
    builder.HasIndex(x => new { x.ShowFrom, x.ShowUntil }).HasDatabaseName("idx_ann_showing");
  }
}

public class AnnouncementReadConfiguration : EntityConfiguration<AnnouncementRead, AnnouncementReadId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<AnnouncementRead> builder)
  {
    base.Configure(builder);
    builder.ToTable("announcement_read");
    builder.Property(x => x.ReadAt).HasDefaultValueSql("now()");
    builder.HasOne<Announcement>().WithMany().HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    builder.HasIndex(x => new { x.AnnouncementId, x.EmployeeId }).IsUnique().HasDatabaseName("ux_ann_read_once");
    builder.HasIndex(x => x.EmployeeId).HasDatabaseName("idx_ann_read_employee");
  }
}
