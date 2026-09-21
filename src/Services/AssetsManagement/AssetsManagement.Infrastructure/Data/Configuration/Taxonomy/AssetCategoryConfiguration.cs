using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetCategoryConfiguration : EntityConfiguration<AssetCategory, AssetCategoryId>
{
  public override void Configure(EntityTypeBuilder<AssetCategory> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_category");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetCategoryId.Of(dbId));

    builder.Property(x => x.AssetClassId)
      .HasConversion(id => id.Value, dbId => AssetClassId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AssetTypeId)
      .HasConversion(id => id!.Value, dbId => AssetTypeId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.ParentCategoryId)
      .HasConversion(id => id!.Value, dbId => AssetCategoryId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.Code)
      .HasConversion(code => code.Value, dbValue => LookupCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(x => x.Name)
      .HasConversion(name => name.Value, dbValue => Name.Of(dbValue, 150))
      .HasMaxLength(150)
      .IsRequired();

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.Path).HasLtree().IsRequired();
    builder.Property(x => x.Depth).IsRequired().HasDefaultValue(0);
    builder.Property(x => x.IsLeaf).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.DisplayOrder).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => new { x.AssetClassId, x.Code }).IsUnique();
    builder.HasIndex(x => x.ParentCategoryId);
    builder.HasIndex(x => x.Path).HasMethod("gist");

    // Target for the composite FK from asset — guarantees the category belongs to the class.
    builder.HasAlternateKey(x => new { x.Id, x.AssetClassId });

    builder.HasOne<AssetClass>()
      .WithMany()
      .HasForeignKey(x => x.AssetClassId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<AssetType>()
      .WithMany()
      .HasForeignKey(x => x.AssetTypeId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<AssetCategory>()
      .WithMany()
      .HasForeignKey(x => x.ParentCategoryId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
