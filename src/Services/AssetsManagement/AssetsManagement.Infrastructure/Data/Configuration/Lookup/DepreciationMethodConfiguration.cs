using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DepreciationMethodConfiguration : EntityConfiguration<DepreciationMethod, DepreciationMethodId>
{
  public override void Configure(EntityTypeBuilder<DepreciationMethod> builder)
  {
    base.Configure(builder);

    builder.ToTable("depreciation_method");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => DepreciationMethodId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
  }
}
