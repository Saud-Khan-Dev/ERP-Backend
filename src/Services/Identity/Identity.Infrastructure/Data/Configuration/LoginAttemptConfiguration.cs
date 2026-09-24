using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class LoginAttemptConfiguration : EntityConfiguration<LoginAttempt, LoginAttemptId>
{
  public override void Configure(EntityTypeBuilder<LoginAttempt> builder)
  {
    base.Configure(builder);

    builder.ToTable("login_attempts");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => LoginAttemptId.Of(dbId));

    // nullable: an attempt against a username that does not exist is still worth recording
    builder.Property(x => x.UserId)
      .HasConversion(id => id!.Value, dbId => UserId.Of(dbId))
      .IsRequired(false);

    builder.Property(x => x.AttemptedUsername).HasMaxLength(Username.MaxLength).IsRequired();
    builder.Property(x => x.Succeeded).IsRequired();
    builder.Property(x => x.FailureReason).HasMaxLength(200).IsRequired(false);

    builder.Property(x => x.IpAddress)
      .HasConversion(ip => ip!.Value, dbValue => IpAddress.OfNullable(dbValue)!)
      .HasMaxLength(IpAddress.MaxLength)
      .IsRequired(false);

    builder.Property(x => x.UserAgent).HasMaxLength(512).IsRequired(false);
    builder.Property(x => x.AttemptedAt).IsRequired();

    builder.HasIndex(x => x.UserId);
    builder.HasIndex(x => x.AttemptedAt);
    builder.HasIndex(x => x.AttemptedUsername);

    // the audit trail outlives the account: no cascade delete, no FK constraint to fight
    builder.HasOne<User>()
      .WithMany()
      .HasForeignKey(x => x.UserId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.SetNull);
  }
}
