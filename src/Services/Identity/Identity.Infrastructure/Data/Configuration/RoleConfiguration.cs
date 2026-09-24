using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RoleConfiguration : EntityConfiguration<Role, RoleId>
{
  public override void Configure(EntityTypeBuilder<Role> builder)
  {
    base.Configure(builder);

    builder.ToTable("roles");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => RoleId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(c => c.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(LookupCode.MaxLength)
      .IsRequired();

    builder.Property(x => x.RoleName)
      .HasColumnName("name")
      .HasConversion(n => n.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsSystem).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.DeletedAt).IsRequired(false);

    builder.Ignore(x => x.IsDeleted);
    builder.Ignore(x => x.IsSuperAdmin);

    builder.HasQueryFilter(x => x.DeletedAt == null);

    builder.HasIndex(x => x.Code).IsUnique();
  }
}
