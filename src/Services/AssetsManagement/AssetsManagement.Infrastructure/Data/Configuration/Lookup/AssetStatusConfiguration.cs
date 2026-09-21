using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetStatusConfiguration : EntityConfiguration<AssetStatus, AssetStatusId>
{
  public override void Configure(EntityTypeBuilder<AssetStatus> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_status");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetStatusId.Of(dbId));

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsTerminal).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.AllowsAssignment).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.Color).HasMaxLength(20).IsRequired(false);
    builder.Property(x => x.DisplayOrder).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.Code).IsUnique();
  }
}
