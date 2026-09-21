using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetDepreciationEntryConfiguration : EntityConfiguration<AssetDepreciationEntry, AssetDepreciationEntryId>
{
  public override void Configure(EntityTypeBuilder<AssetDepreciationEntry> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_depreciation_entry");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetDepreciationEntryId.Of(dbId));

    builder.Property(x => x.ScheduleId).HasConversion(id => id.Value, dbId => AssetDepreciationScheduleId.Of(dbId)).IsRequired();
    builder.Property(x => x.PeriodStart).IsRequired();
    builder.Property(x => x.PeriodEnd).IsRequired();
    builder.Property(x => x.OpeningBookValue).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.DepreciationAmount).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.AccumulatedDepreciation).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.BookValueAfter).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.Posted).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.PostedAt).IsRequired(false);
    builder.Property(x => x.PostedBy).IsRequired(false);
    builder.Property(x => x.ReversedAt).IsRequired(false);
    builder.Property(x => x.ReversedBy).IsRequired(false);

    builder.Ignore(x => x.IsReversed);

    builder.HasIndex(x => x.ScheduleId);
    builder.HasIndex(x => x.PeriodStart);
    builder.HasIndex(x => new { x.ScheduleId, x.PeriodStart, x.PeriodEnd }).IsUnique();

    builder.ToTable(t =>
    {
      t.HasCheckConstraint("ck_asset_depreciation_entry_period", "period_end > period_start");
      t.HasCheckConstraint("ck_asset_depreciation_entry_amount", "depreciation_amount >= 0");
    });
  }
}
