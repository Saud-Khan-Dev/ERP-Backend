using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AssetLifecycleEventConfiguration : EntityConfiguration<AssetLifecycleEvent, AssetLifecycleEventId>
{
  public override void Configure(EntityTypeBuilder<AssetLifecycleEvent> builder)
  {
    base.Configure(builder);

    builder.ToTable("asset_lifecycle_event");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => AssetLifecycleEventId.Of(dbId));

    builder.Property(x => x.AssetId).HasConversion(id => id.Value, dbId => AssetId.Of(dbId)).IsRequired();
    builder.Property(x => x.EventTypeId).HasConversion(id => id.Value, dbId => LifecycleEventTypeId.Of(dbId)).IsRequired();
    builder.Property(x => x.EventDate).IsRequired();
    builder.Property(x => x.FromStatusId).HasConversion(id => id!.Value, dbId => AssetStatusId.Of(dbId)).IsRequired(false);
    builder.Property(x => x.ToStatusId).HasConversion(id => id!.Value, dbId => AssetStatusId.Of(dbId)).IsRequired(false);
    builder.Property(x => x.PerformedBy).IsRequired(false);
    builder.Property(x => x.Notes).IsRequired(false);
    builder.Property(x => x.Details).HasJsonb().IsRequired(false);

    builder.HasIndex(x => x.AssetId);
    builder.HasIndex(x => x.EventTypeId);
    builder.HasIndex(x => x.EventDate);
    builder.HasIndex(x => x.Details).HasMethod("gin");

    builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).IsRequired().OnDelete(DeleteBehavior.Cascade);
    builder.HasOne<LifecycleEventType>().WithMany().HasForeignKey(x => x.EventTypeId).IsRequired().OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AssetStatus>().WithMany().HasForeignKey(x => x.FromStatusId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AssetStatus>().WithMany().HasForeignKey(x => x.ToStatusId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
  }
}
