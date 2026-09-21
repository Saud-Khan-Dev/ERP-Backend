using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

public class AssetAttributeValueConfiguration : EntityConfiguration<AssetAttributeValue, AssetAttributeValueId>
{
  public override void Configure(EntityTypeBuilder<AssetAttributeValue> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_attribute_value");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetAttributeValueId.Of(dbId));

    builder.Property(x => x.AssetId)
      .HasConversion(id => id.Value, dbId => AssetId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AttributeDefinitionId)
      .HasConversion(id => id.Value, dbId => AttributeDefinitionId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AttributeCode).HasMaxLength(100).IsRequired();

    builder.Property(x => x.ValueText).IsRequired(false);
    builder.Property(x => x.ValueNumber).HasPrecision(18, 4).IsRequired(false);
    builder.Property(x => x.ValueBoolean).IsRequired(false);
    builder.Property(x => x.ValueDate).IsRequired(false);
    builder.Property(x => x.ValueDatetime).IsRequired(false);
    builder.Property(x => x.ValueJson).HasJsonb().IsRequired(false);

    builder.Property(x => x.OptionValueId)
      .HasConversion(id => id!.Value, dbId => OptionSetValueId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.ValueIndex).IsRequired().HasDefaultValue((short)0);

    // Generated from value_text for full-text search (shadow property: the domain never touches it).
    builder.Property<NpgsqlTsVector>("SearchVector")
      .HasColumnName("search_vector")
      .IsGeneratedTsVectorColumn("english", "ValueText");

    builder.HasIndex(x => x.AssetId);
    builder.HasIndex(x => new { x.AttributeDefinitionId, x.ValueText });
    builder.HasIndex(x => new { x.AttributeDefinitionId, x.ValueNumber });
    builder.HasIndex(x => new { x.AttributeDefinitionId, x.ValueDate });
    builder.HasIndex(x => new { x.AttributeDefinitionId, x.OptionValueId });
    builder.HasIndex(x => new { x.AssetId, x.AttributeDefinitionId, x.ValueIndex }).IsUnique();
    builder.HasIndex("SearchVector").HasMethod("gin");

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x => x.AttributeDefinitionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<OptionSetValue>().WithMany().HasForeignKey(x => x.OptionValueId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
  }
}
