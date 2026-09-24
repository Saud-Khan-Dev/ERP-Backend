using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PermissionModuleConfiguration : EntityConfiguration<PermissionModule, PermissionModuleId>
{
  public override void Configure(EntityTypeBuilder<PermissionModule> builder)
  {
    base.Configure(builder);

    builder.ToTable("permission_modules");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => PermissionModuleId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(c => c.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(LookupCode.MaxLength)
      .IsRequired();

    builder.Property(x => x.ModuleName)
      .HasColumnName("name")
      .HasConversion(n => n.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.HasIndex(x => x.Code).IsUnique();
  }
}

public class PermissionConfiguration : EntityConfiguration<Permission, PermissionId>
{
  public override void Configure(EntityTypeBuilder<Permission> builder)
  {
    base.Configure(builder);

    builder.ToTable("permissions");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => PermissionId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(c => c.Value, dbValue => PermissionCode.Of(dbValue))
      .HasMaxLength(PermissionCode.MaxLength)
      .IsRequired();

    builder.Property(x => x.ModuleId)
      .HasConversion(id => id.Value, dbId => PermissionModuleId.Of(dbId))
      .IsRequired();

    // stored as a string from a closed enum, so EDIT can never drift into UPDATE
    builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20).IsRequired();

    builder.Property(x => x.PermissionName)
      .HasColumnName("name")
      .HasConversion(n => n.Value, dbValue => Name.Of(dbValue))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
    // one permission per module+action: no duplicate ASSETS.VIEW under two different names
    builder.HasIndex(x => new { x.ModuleId, x.Action }).IsUnique();
    builder.HasIndex(x => x.Action);

    builder.HasOne<PermissionModule>()
      .WithMany()
      .HasForeignKey(x => x.ModuleId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);
  }
}
