using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetTypeConfiguration : EntityConfiguration<AssetType, AssetTypeId>
{
  public override void Configure(EntityTypeBuilder<AssetType> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_type");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetTypeId.Of(dbId));

    builder.Property(x => x.AssetClassId)
      .HasConversion(id => id.Value, dbId => AssetClassId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 100))
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.IsDepreciable).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.RequiresLocation).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.RequiresCustodian).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.DisplayOrder).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => new { x.AssetClassId, x.Code }).IsUnique();

    // Target for the composite FK from asset — guarantees the type belongs to the class.
    builder.HasAlternateKey(x => new { x.Id, x.AssetClassId });

    builder.HasOne<AssetClass>()
      .WithMany()
      .HasForeignKey(x => x.AssetClassId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);
  }
}
