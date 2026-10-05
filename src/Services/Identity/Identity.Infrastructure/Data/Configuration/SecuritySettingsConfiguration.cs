using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SecuritySettingsConfiguration : EntityConfiguration<SecuritySettings, SecuritySettingsId>
{
  public override void Configure(EntityTypeBuilder<SecuritySettings> builder)
  {
    base.Configure(builder);

    builder.ToTable("security_settings");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => SecuritySettingsId.Of(dbId));

    builder.Property(x => x.PasswordMinimumLength).IsRequired();
    builder.Property(x => x.PasswordRequireUppercase).IsRequired();
    builder.Property(x => x.PasswordRequireLowercase).IsRequired();
    builder.Property(x => x.PasswordRequireDigit).IsRequired();
    builder.Property(x => x.PasswordRequireNonAlphanumeric).IsRequired();
    builder.Property(x => x.MaxFailedLoginAttempts).IsRequired();
    builder.Property(x => x.LockoutMinutes).IsRequired();
    builder.Property(x => x.AccessTokenMinutes).IsRequired();
    builder.Property(x => x.RefreshTokenDays).IsRequired();

    builder.Ignore(x => x.PasswordPolicy);
    builder.Ignore(x => x.LockoutDuration);
    builder.Ignore(x => x.AccessTokenLifetime);
    builder.Ignore(x => x.RefreshTokenLifetime);
  }
}
