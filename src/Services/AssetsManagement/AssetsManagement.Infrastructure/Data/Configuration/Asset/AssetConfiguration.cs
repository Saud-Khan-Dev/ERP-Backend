using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetConfiguration : EntityConfiguration<Asset, AssetId>
{
  public override void Configure(EntityTypeBuilder<Asset> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetId.Of(dbId));

    builder.Property(x => x.AssetCode)
      .HasConversion(code => code.Value, dbValue => AssetCode.Of(dbValue))
      .HasMaxLength(50)
      .IsRequired();

    // Complex property (not a converter) so list queries can translate `a.Name.Value.Contains(term)` to SQL.
    builder.ComplexProperty(x => x.Name, name =>
    {
      name.Property(n => n.Value)
        .HasColumnName("name")
        .HasMaxLength(200)
        .IsRequired();
    });

    builder.Property(x => x.Description).IsRequired(false);
    builder.Property(x => x.Ownership).HasEnumString().IsRequired();

    builder.Property(x => x.AssetClassId).HasConversion(id => id.Value, dbId => AssetClassId.Of(dbId)).IsRequired();
    builder.Property(x => x.AssetTypeId).HasConversion(id => id.Value, dbId => AssetTypeId.Of(dbId)).IsRequired();
    builder.Property(x => x.CategoryId).HasConversion(id => id.Value, dbId => AssetCategoryId.Of(dbId)).IsRequired();
    builder.Property(x => x.StatusId).HasConversion(id => id.Value, dbId => AssetStatusId.Of(dbId)).IsRequired();

    builder.Property(x => x.DepartmentId).IsRequired(false);
    builder.Property(x => x.CustodianId).IsRequired(false);

    builder.Property(x => x.CurrentLocationId)
      .HasConversion(id => id!.Value, dbId => LocationId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.Barcode).HasMaxLength(100).IsRequired(false);

    // Dynamic values: SOURCE OF TRUTH. Mapped through the backing field so the domain keeps a read-only view.
    builder.Ignore(x => x.ExtraAttributes);
    builder.Property<Dictionary<string, JsonElement>>("_extraAttributes")
      .HasColumnName("extra_attributes")
      .HasJsonbDictionary()
      .IsRequired();

    builder.Property(x => x.AttributesValidatedAt).IsRequired(false);

    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.DeletedAt).IsRequired(false);
    builder.Property(x => x.DeletedBy).HasMaxLength(100).IsRequired(false);
    builder.Ignore(x => x.IsDeleted);

    // EF Core optimistic concurrency token backed by PostgreSQL's xmin system column.
    builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

    // Soft delete
    builder.HasQueryFilter(x => x.DeletedAt == null);

    builder.HasIndex(x => x.AssetCode).IsUnique();
    builder.HasIndex(x => x.AssetClassId);
    builder.HasIndex(x => x.AssetTypeId);
    builder.HasIndex(x => x.CategoryId);
    builder.HasIndex(x => x.StatusId);
    builder.HasIndex(x => x.DepartmentId);
    builder.HasIndex(x => x.CustodianId);
    builder.HasIndex(x => x.CurrentLocationId);
    builder.HasIndex(x => x.Barcode).IsUnique();
    builder.HasIndex("_extraAttributes")
      .HasDatabaseName("ix_asset_extra_attributes")
      .HasMethod("gin")
      .HasOperators("jsonb_path_ops");

    builder.HasOne<AssetClass>()
      .WithMany()
      .HasForeignKey(x => x.AssetClassId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);

    // Composite FKs: guarantee type and category actually belong to the class.
    builder.HasOne<AssetType>()
      .WithMany()
      .HasForeignKey(x => new { x.AssetTypeId, x.AssetClassId })
      .HasPrincipalKey(t => new { t.Id, t.AssetClassId })
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<AssetCategory>()
      .WithMany()
      .HasForeignKey(x => new { x.CategoryId, x.AssetClassId })
      .HasPrincipalKey(c => new { c.Id, c.AssetClassId })
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<AssetStatus>()
      .WithMany()
      .HasForeignKey(x => x.StatusId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Location>()
      .WithMany()
      .HasForeignKey(x => x.CurrentLocationId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
