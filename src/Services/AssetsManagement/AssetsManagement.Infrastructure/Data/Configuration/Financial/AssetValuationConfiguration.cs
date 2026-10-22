using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetValuationConfiguration : EntityConfiguration<AssetValuation, AssetValuationId>
{
  public override void Configure(EntityTypeBuilder<AssetValuation> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_valuation_history");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetValuationId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.ValuationDate).IsRequired();
    builder.Property(x => x.Value).HasPrecision(18, 2).IsRequired();

    builder.Property(x => x.CurrencyCode)
      .HasConversion(currency => currency.Value, dbValue => Currency.Of(dbValue))
      .HasColumnType("char(3)")
      .IsRequired();

    builder.Property(x => x.ValuationMethod).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.ValuedByName).HasMaxLength(150).IsRequired(false);
    builder.Property(x => x.RecordedBy).IsRequired(false);
    builder.Property(x => x.RecordedByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.Notes).IsRequired(false);

    builder.HasIndex(x => x.AssetId);
    builder.HasIndex(x => x.ValuationDate);
    builder.HasIndex(x => new { x.AssetId, x.ValuationDate }).IsUnique();

    // Restrict: an asset is only ever soft-deleted, and a hard delete must never take its history with it
    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CurrencyLookup>().WithMany().HasForeignKey(x => x.CurrencyCode).IsRequired().OnDelete(DeleteBehavior.Restrict);
  }
}
