using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SessionConfiguration : EntityConfiguration<Session, SessionId>
{
  public override void Configure(EntityTypeBuilder<Session> builder)
  {
    base.Configure(builder);

    builder.ToTable("sessions");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => SessionId.Of(dbId));

    builder.Property(x => x.UserId)
      .HasConversion(id => id.Value, dbId => UserId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.RefreshTokenHash).IsRequired();

    builder.Property(x => x.IpAddress)
      .HasConversion(ip => ip!.Value, dbValue => IpAddress.OfNullable(dbValue)!)
      .HasMaxLength(IpAddress.MaxLength)
      .IsRequired(false);

    builder.Property(x => x.UserAgent).HasMaxLength(512).IsRequired(false);
    builder.Property(x => x.IssuedAt).IsRequired();
    builder.Property(x => x.ExpiresAt).IsRequired();
    builder.Property(x => x.RevokedAt).IsRequired(false);
    builder.Property(x => x.RevokedReason).HasMaxLength(200).IsRequired(false);

    builder.Property(x => x.ReplacedBySessionId)
      .HasConversion(id => id!.Value, dbId => SessionId.Of(dbId))
      .IsRequired(false);

    builder.Ignore(x => x.IsRevoked);

    // a refresh token is looked up by its hash on every refresh
    builder.HasIndex(x => x.RefreshTokenHash).IsUnique();
    builder.HasIndex(x => x.UserId);
    builder.HasIndex(x => x.ExpiresAt);

    builder.HasOne<User>()
      .WithMany()
      .HasForeignKey(x => x.UserId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Session>()
      .WithMany()
      .HasForeignKey(x => x.ReplacedBySessionId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
