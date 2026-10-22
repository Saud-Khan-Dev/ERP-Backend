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
    // no model default: Proposed is the enum's zero, and a default would turn a new proposal into an approval (the
    // migration sets the disposals recorded before the two steps to Approved)
    builder.Property(x => x.State).HasEnumString().IsRequired();
    builder.Property(x => x.ToStatusId).HasConversion(id => id!.Value, dbId => AssetStatusId.Of(dbId)).IsRequired(false);
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
    builder.Property(x => x.ApprovedByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.ApprovedAt).IsRequired(false);
    builder.Property(x => x.ProposedBy).IsRequired(false);
    builder.Property(x => x.ProposedByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.ProposedAt).IsRequired(false);
    builder.Property(x => x.DecidedBy).IsRequired(false);
    builder.Property(x => x.DecidedByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.DecidedAt).IsRequired(false);
    builder.Property(x => x.DecisionNote).HasMaxLength(1000).IsRequired(false);
    builder.Property(x => x.CancelledBy).IsRequired(false);
    builder.Property(x => x.CancelledByName).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.CancelledAt).IsRequired(false);
    builder.Property(x => x.CancellationReason).HasMaxLength(1000).IsRequired(false);
    builder.Ignore(x => x.IsLive);

    // one live disposal per asset: a proposal waiting, or the approved one; rejected, withdrawn and cancelled ones stay
    builder.HasIndex(x => x.AssetId).IsUnique().HasFilter("state IN ('Proposed', 'Approved')").HasDatabaseName("ux_asset_disposal_live");
    builder.HasIndex(x => new { x.AssetId, x.State });

    // Restrict: an asset is only ever soft-deleted, and a hard delete must never take its history with it
    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<DisposalMethod>().WithMany().HasForeignKey(x => x.DisposalMethodId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AssetStatus>().WithMany().HasForeignKey(x => x.ToStatusId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<CurrencyLookup>().WithMany().HasForeignKey(x => x.CurrencyCode).IsRequired(false).OnDelete(DeleteBehavior.Restrict);

    builder.ToTable(t => t.HasCheckConstraint("ck_asset_disposal_currency", "disposal_value IS NULL OR currency_code IS NOT NULL"));
  }
}
