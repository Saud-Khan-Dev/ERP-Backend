using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AdminActivityConfiguration : EntityConfiguration<AdminActivity, ActivityId>
{
  public override void Configure(EntityTypeBuilder<AdminActivity> builder)
  {
    base.Configure(builder);

    builder.ToTable("admin_activities");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => ActivityId.Of(dbId));

    builder.Property(x => x.ActorUserId)
      .HasConversion(id => id.Value, dbId => UserId.Of(dbId))
      .IsRequired();
    builder.Property(x => x.ActorName).HasMaxLength(150).IsRequired();

    builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(40).IsRequired();
    builder.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(20).IsRequired();
    builder.Property(x => x.TargetId).IsRequired(false);
    builder.Property(x => x.TargetLabel).HasMaxLength(200).IsRequired(false);
    builder.Property(x => x.Detail).HasMaxLength(500).IsRequired(false);

    builder.Property(x => x.IpAddress)
      .HasConversion(ip => ip!.Value, dbValue => IpAddress.OfNullable(dbValue)!)
      .HasMaxLength(IpAddress.MaxLength)
      .IsRequired(false);
    builder.Property(x => x.UserAgent).HasMaxLength(512).IsRequired(false);

    builder.Property(x => x.OccurredAt).IsRequired();

    builder.HasIndex(x => x.OccurredAt);
    builder.HasIndex(x => x.ActorUserId);
    builder.HasIndex(x => x.TargetId);
    builder.HasIndex(x => x.Action);

    // the trail outlives the accounts it mentions
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
  }
}
