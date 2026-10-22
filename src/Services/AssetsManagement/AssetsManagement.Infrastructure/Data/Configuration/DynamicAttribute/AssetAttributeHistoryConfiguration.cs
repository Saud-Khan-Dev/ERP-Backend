using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// The field-change archive (written until 2026-10-21; the audit trail records field changes since).
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
    builder.Property(x => x.ChangedByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.ChangeReason).IsRequired(false);

    builder.HasIndex(x => new { x.AssetId, x.ChangedAt });
    builder.HasIndex(x => x.AttributeDefinitionId);

    // Restrict: an asset is only ever soft-deleted, and a hard delete must never take its history with it
    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    // no foreign key to the field: deleting a field must never erase what was recorded about it (the table is the
    // read-only archive of field changes before the audit trail took over on 2026-10-21)
  }
}
