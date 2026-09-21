using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AttributeGroupConfiguration : EntityConfiguration<AttributeGroup, AttributeGroupId>
{
  public override void Configure(EntityTypeBuilder<AttributeGroup> builder)
  {
    base.Configure(builder);

    builder.ToTable("attribute_group");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AttributeGroupId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 150))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.DisplayOrder).IsRequired(false);
    builder.Property(x => x.IsCollapsible).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
  }
}
