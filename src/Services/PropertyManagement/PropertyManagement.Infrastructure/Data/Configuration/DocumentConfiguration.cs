using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PropertyDocumentConfiguration : EntityConfiguration<PropertyDocument, DocumentId>
{
  public override void Configure(EntityTypeBuilder<PropertyDocument> builder)
  {
    base.Configure(builder);
    builder.ToTable("property_document", t =>
    {
      // property_id is always set, except for an owner's own papers (CNIC copy ...)
      t.HasCheckConstraint("ck_property_document_property", "entity_type = 'OWNER' OR property_id IS NOT NULL");
      t.HasCheckConstraint("ck_property_document_version", "version_no >= 1");
    });

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, value => DocumentId.Of(value));
    builder.Property(x => x.PropertyId).HasConversion(id => id!.Value, value => PropertyId.Of(value));
    builder.Property(x => x.DocumentTypeId).HasMasterId().IsRequired();
    builder.Property(x => x.EntityType).HasUpperSnakeEnum(40).IsRequired().HasDefaultValue(DocumentEntityType.Property);
    builder.Property(x => x.EntityId).IsRequired();
    builder.Property(x => x.Title).HasMaxLength(200);
    builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
    builder.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
    builder.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
    builder.Property(x => x.ChecksumSha256).HasColumnType("char(64)").IsRequired();
    builder.Property(x => x.ReferenceNo).HasMaxLength(100);
    builder.Property(x => x.VersionNo).IsRequired().HasDefaultValue(1);
    builder.Property(x => x.SupersedesDocumentId).HasConversion(id => id!.Value, value => DocumentId.Of(value));
    builder.Property(x => x.IsConfidential).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.UploadedAt).IsRequired();
    builder.Property(x => x.UploadedBy).IsRequired();

    builder.HasIndex(x => new { x.PropertyId, x.DocumentTypeId });
    builder.HasIndex(x => new { x.EntityType, x.EntityId });
    builder.HasIndex(x => x.SupersedesDocumentId).IsUnique();

    builder.HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<PropertyDocument>().WithMany().HasForeignKey(x => x.SupersedesDocumentId).OnDelete(DeleteBehavior.Restrict);
  }
}
