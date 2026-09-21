using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetAcquisitionConfiguration : EntityConfiguration<AssetAcquisition, AssetAcquisitionId>
{
  public override void Configure(EntityTypeBuilder<AssetAcquisition> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_acquisition");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetAcquisitionId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.AcquisitionDate).IsRequired();
    builder.Property(x => x.AcquisitionCost).HasPrecision(18, 2).IsRequired();

    builder.Property(x => x.CurrencyCode)
      .HasConversion(currency => currency.Value, dbValue => Currency.Of(dbValue))
      .HasColumnType("char(3)")
      .IsRequired();

    builder.Property(x => x.ExchangeRate).HasPrecision(18, 6).IsRequired(false);
    builder.Property(x => x.BaseCurrencyCost).HasPrecision(18, 2).IsRequired(false);
    builder.Property(x => x.SupplierId).IsRequired(false);
    builder.Property(x => x.PurchaseReference).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.AcquisitionType).HasEnumString().IsRequired();
    builder.Property(x => x.WarrantyStartDate).IsRequired(false);
    builder.Property(x => x.WarrantyExpiryDate).IsRequired(false);

    builder.HasIndex(x => x.AssetId).IsUnique();

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<CurrencyLookup>().WithMany().HasForeignKey(x => x.CurrencyCode).IsRequired().OnDelete(DeleteBehavior.Restrict);

    builder.ToTable(t =>
    {
      t.HasCheckConstraint("ck_asset_acquisition_cost", "acquisition_cost >= 0");
      t.HasCheckConstraint("ck_asset_acquisition_warranty", "warranty_expiry_date IS NULL OR warranty_expiry_date >= acquisition_date");
    });
  }
}
