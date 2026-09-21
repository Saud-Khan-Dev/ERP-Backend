using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetAttributeHistoryConfiguration : EntityConfiguration<AssetAttributeHistory, AssetAttributeHistoryId>
{
  public override void Configure(EntityTypeBuilder<AssetAttributeHistory> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_attribute_history");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetAttributeHistoryId.Of(dbId));

    builder.Property(x => x.AssetId)
      .HasConversion(id => id.Value, dbId => AssetId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AttributeDefinitionId)
      .HasConversion(id => id.Value, dbId => AttributeDefinitionId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AttributeCode).HasMaxLength(100).IsRequired();
    builder.Property(x => x.OldValue).HasJsonb().IsRequired(false);
    builder.Property(x => x.NewValue).HasJsonb().IsRequired(false);
    builder.Property(x => x.ChangedAt).IsRequired();
    builder.Property(x => x.ChangedBy).IsRequired(false);
    builder.Property(x => x.ChangeReason).IsRequired(false);

    builder.HasIndex(x => new { x.AssetId, x.ChangedAt });
    builder.HasIndex(x => x.AttributeDefinitionId);

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x => x.AttributeDefinitionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
  }
}
