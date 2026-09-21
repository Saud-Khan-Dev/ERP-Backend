using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AttributeAssignmentConfiguration : EntityConfiguration<AttributeAssignment, AttributeAssignmentId>
{
  public override void Configure(EntityTypeBuilder<AttributeAssignment> builder)
  {
    base.Configure(builder);

    builder.ToTable("attribute_assignment");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AttributeAssignmentId.Of(dbId));

    builder.Property(x => x.AttributeDefinitionId)
      .HasConversion(id => id.Value, dbId => AttributeDefinitionId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.Scope).HasEnumString().IsRequired();

    builder.Property(x => x.AssetClassId)
      .HasConversion(id => id!.Value, dbId => AssetClassId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.AssetTypeId)
      .HasConversion(id => id!.Value, dbId => AssetTypeId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.CategoryId)
      .HasConversion(id => id!.Value, dbId => AssetCategoryId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.AssetId)
      .HasConversion(id => id!.Value, dbId => AssetId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.AttributeGroupId)
      .HasConversion(id => id!.Value, dbId => AttributeGroupId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.LabelOverride).HasMaxLength(150).IsRequired(false);
    builder.Property(x => x.IsRequired).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsReadonly).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsSearchable).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsFilterable).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsVisibleInList).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.InheritToChildren).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.DefaultValue).HasJsonb().IsRequired(false);
    builder.Property(x => x.DisplayOrder).IsRequired(false);

    builder.Property(x => x.DependsOnAssignmentId)
      .HasConversion(id => id!.Value, dbId => AttributeAssignmentId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.DependsOnValue).HasJsonb().IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.Ignore(x => x.IsProjected);

    builder.HasIndex(x => x.AttributeDefinitionId);
    builder.HasIndex(x => new { x.Scope, x.AssetClassId });
    builder.HasIndex(x => new { x.Scope, x.AssetTypeId });
    builder.HasIndex(x => new { x.Scope, x.CategoryId });
    builder.HasIndex(x => x.AssetId);

    // One assignment per attribute per target.
    builder.HasIndex(x => new { x.AssetClassId, x.AttributeDefinitionId }).IsUnique().HasFilter("asset_class_id IS NOT NULL");
    builder.HasIndex(x => new { x.AssetTypeId, x.AttributeDefinitionId }).IsUnique().HasFilter("asset_type_id IS NOT NULL");
    builder.HasIndex(x => new { x.CategoryId, x.AttributeDefinitionId }).IsUnique().HasFilter("category_id IS NOT NULL");
    builder.HasIndex(x => new { x.AssetId, x.AttributeDefinitionId }).IsUnique().HasFilter("asset_id IS NOT NULL");

    builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x => x.AttributeDefinitionId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AssetClass>().WithMany().HasForeignKey(x => x.AssetClassId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AssetType>().WithMany().HasForeignKey(x => x.AssetTypeId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AssetCategory>().WithMany().HasForeignKey(x => x.CategoryId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired(false).OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<AttributeGroup>().WithMany().HasForeignKey(x => x.AttributeGroupId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    builder.HasOne<AttributeAssignment>().WithMany().HasForeignKey(x => x.DependsOnAssignmentId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);

    builder.ToTable(t => t.HasCheckConstraint("ck_attribute_assignment_scope",
      "(scope = 'AssetClass' AND asset_class_id IS NOT NULL AND asset_type_id IS NULL AND category_id IS NULL AND asset_id IS NULL) OR " +
      "(scope = 'AssetType'  AND asset_type_id  IS NOT NULL AND asset_class_id IS NULL AND category_id IS NULL AND asset_id IS NULL) OR " +
      "(scope = 'Category'   AND category_id    IS NOT NULL AND asset_class_id IS NULL AND asset_type_id IS NULL AND asset_id IS NULL) OR " +
      "(scope = 'Asset'      AND asset_id       IS NOT NULL AND asset_class_id IS NULL AND asset_type_id IS NULL AND category_id IS NULL)"));
  }
}
