using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class NotesheetConfiguration : EntityConfiguration<Notesheet, NotesheetId>
{
  public override void Configure(EntityTypeBuilder<Notesheet> builder)
  {
    base.Configure(builder);
    builder.ToTable("notesheet", t =>
    {
      t.HasCheckConstraint("ck_ns_file", "(file_path IS NULL) = (file_name IS NULL)");
      t.HasCheckConstraint("ck_ns_holder", "(status IN ('approved','rejected','withdrawn')) = (holder_id IS NULL)");
    });
    builder.Ignore(x => x.Number);
    builder.Property(x => x.Subject).IsRequired();
    builder.Property(x => x.Body).IsRequired().Text();
    builder.Property(x => x.Status).HasDefaultValue(NotesheetStatus.Draft);

    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.WriterId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.HolderId).OnDelete(DeleteBehavior.NoAction);
    builder.HasMany(x => x.Movements).WithOne().HasForeignKey(m => m.NotesheetId).OnDelete(DeleteBehavior.NoAction);
    builder.Navigation(x => x.Movements).UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.HasIndex(x => x.Serial).IsUnique().HasDatabaseName("ux_ns_serial");
    builder.HasIndex(x => x.WriterId).HasDatabaseName("idx_ns_writer");
    builder.HasIndex(x => x.HolderId).HasDatabaseName("idx_ns_holder");
  }
}

public class NotesheetMovementConfiguration : EntityConfiguration<NotesheetMovement, NotesheetMovementId>
{
  protected override AuditColumns Audit => AuditColumns.None;

  public override void Configure(EntityTypeBuilder<NotesheetMovement> builder)
  {
    base.Configure(builder);
    builder.ToTable("notesheet_movement", t => t.HasCheckConstraint("ck_nsm_file", "(file_path IS NULL) = (file_name IS NULL)"));
    builder.Property(x => x.Remarks).Text();
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.FromId).OnDelete(DeleteBehavior.NoAction);
    builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.ToId).OnDelete(DeleteBehavior.NoAction);
    builder.HasIndex(x => new { x.NotesheetId, x.At }).HasDatabaseName("idx_nsm_notesheet");
    builder.HasIndex(x => x.FromId).HasDatabaseName("idx_nsm_from");
    builder.HasIndex(x => x.ToId).HasDatabaseName("idx_nsm_to");
  }
}
