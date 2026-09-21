using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetDepreciationScheduleConfiguration : EntityConfiguration<AssetDepreciationSchedule, AssetDepreciationScheduleId>
{
  public override void Configure(EntityTypeBuilder<AssetDepreciationSchedule> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_depreciation_schedule");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetDepreciationScheduleId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.MethodId).HasConversion(id => id.Value, dbId => DepreciationMethodId.Of(dbId)).IsRequired();
    builder.Property(x => x.UsefulLifeMonths).IsRequired();
    builder.Property(x => x.SalvageValue).HasPrecision(18, 2).IsRequired().HasDefaultValue(0m);
    builder.Property(x => x.DepreciableBase).HasPrecision(18, 2).IsRequired();
    builder.Property(x => x.DecliningRate).HasPrecision(9, 6).IsRequired(false);
    builder.Property(x => x.StartDate).IsRequired();
    builder.Property(x => x.EndDate).IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

    builder.HasIndex(x => x.AssetId);
    // Partial unique: only one live schedule per asset.
    builder.HasIndex(x => x.AssetId)
      .IsUnique()
      .HasDatabaseName("ux_asset_depreciation_schedule_active")
      .HasFilter("is_active");

    builder.HasMany(x => x.Entries)
      .WithOne()
      .HasForeignKey(e => e.ScheduleId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(x => x.Entries)
      .HasField("_entries")
      .UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<DepreciationMethod>().WithMany().HasForeignKey(x => x.MethodId).IsRequired().OnDelete(DeleteBehavior.Restrict);

    builder.ToTable(t =>
    {
      t.HasCheckConstraint("ck_asset_depreciation_schedule_life", "useful_life_months > 0");
      t.HasCheckConstraint("ck_asset_depreciation_schedule_salvage", "salvage_value >= 0");
    });
  }
}
