using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetDisposalConfiguration : EntityConfiguration<AssetDisposal, AssetDisposalId>
{
  public override void Configure(EntityTypeBuilder<AssetDisposal> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_disposal");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetDisposalId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.DisposalDate).IsRequired();
    builder.Property(x => x.DisposalMethodId).HasConversion(id => id.Value, dbId => DisposalMethodId.Of(dbId)).IsRequired();
    builder.Property(x => x.DisposalValue).HasPrecision(18, 2).IsRequired(false);

    builder.Property(x => x.CurrencyCode)
      .HasConversion(currency => currency!.Value, dbValue => Currency.Of(dbValue))
      .HasColumnType("char(3)")
      .IsRequired(false);

    builder.Property(x => x.NetBookValueAtDisposal).HasPrecision(18, 2).IsRequired(false);
    builder.Property(x => x.GainLoss).HasPrecision(18, 2).IsRequired(false);
    builder.Property(x => x.BuyerInfo).IsRequired(false);
    builder.Property(x => x.Reason).IsRequired(false);
    builder.Property(x => x.ApprovedBy).IsRequired(false);
    builder.Property(x => x.ApprovedAt).IsRequired(false);

    builder.HasIndex(x => x.AssetId).IsUnique();

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<DisposalMethod>().WithMany().HasForeignKey(x => x.DisposalMethodId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CurrencyLookup>().WithMany().HasForeignKey(x => x.CurrencyCode).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

    builder.ToTable(t => t.HasCheckConstraint("ck_asset_disposal_currency", "disposal_value IS NULL OR currency_code IS NOT NULL"));
  }
}
