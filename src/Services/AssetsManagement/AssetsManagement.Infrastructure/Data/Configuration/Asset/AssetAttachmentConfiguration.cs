using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetAttachmentConfiguration : EntityConfiguration<AssetAttachment, AssetAttachmentId>
{
  public override void Configure(EntityTypeBuilder<AssetAttachment> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_attachment");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetAttachmentId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.AttachmentType).HasEnumString().IsRequired();
    builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
    builder.Property(x => x.FileSize).IsRequired();
    builder.Property(x => x.StoragePath).IsRequired();
    builder.Property(x => x.ChecksumSha256).HasColumnType("char(64)").IsRequired(false);
    builder.Property(x => x.IsPrimaryImage).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.Title).HasMaxLength(200).IsRequired(false);

    builder.HasIndex(x => x.AssetId);
    builder.HasIndex(x => new { x.AssetId, x.AttachmentType });

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
  }
}
