using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserConfiguration : EntityConfiguration<User, UserId>
{
  public override void Configure(EntityTypeBuilder<User> builder)
  {
    base.Configure(builder);

    builder.ToTable("users");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => UserId.Of(dbId));

    builder.Property(x => x.Username)
      .HasConversion(u => u.Value, dbValue => Username.Of(dbValue))
      .HasMaxLength(Username.MaxLength)
      .IsRequired();

    builder.Property(x => x.Email)
      .HasConversion(e => e.Value, dbValue => EmailAddress.Of(dbValue))
      .HasMaxLength(EmailAddress.MaxLength)
      .IsRequired();

    // Complex property rather than a converter so `DisplayName.Value.Contains(...)` translates to
    // SQL — username and email stay converters because they carry unique indexes and are only ever
    // matched exactly.
    builder.ComplexProperty(x => x.DisplayName, name =>
    {
      name.Property(n => n.Value)
        .HasColumnName("display_name")
        .HasMaxLength(150)
        .IsRequired();
    });

    // never selected into a DTO; the wrapper keeps it out of logs and responses
    builder.Property(x => x.PasswordHash)
      .HasConversion(h => h.Value, dbValue => PasswordHash.Of(dbValue))
      .IsRequired();

    builder.Property(x => x.EmployeeId).IsRequired(false);

    builder.Property(x => x.EmployeeCode)
      .HasConversion(c => c!.Value, dbValue => EmployeeCode.Of(dbValue))
      .HasMaxLength(EmployeeCode.MaxLength)
      .IsRequired(false);
    builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
    builder.Property(x => x.IsAuthorizedOfficer).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.EmailVerifiedAt).IsRequired(false);

    builder.Property(x => x.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
    builder.Property(x => x.LockedUntil).IsRequired(false);

    builder.Property(x => x.PasswordChangedAt).IsRequired(false);
    builder.Property(x => x.MustChangePassword).IsRequired().HasDefaultValue(false);

    builder.Property(x => x.MfaEnabled).IsRequired().HasDefaultValue(false);
    builder.Property(x => x.MfaType).HasConversion<string>().HasMaxLength(20).IsRequired(false);
    builder.Property(x => x.MfaSecret).IsRequired(false);

    builder.Property(x => x.LastLoginAt).IsRequired(false);
    builder.Property(x => x.LastLoginIp)
      .HasConversion(ip => ip!.Value, dbValue => IpAddress.OfNullable(dbValue)!)
      .HasMaxLength(IpAddress.MaxLength)
      .IsRequired(false);

    builder.Property(x => x.DeletedAt).IsRequired(false);
    builder.Property(x => x.DeletedBy).HasMaxLength(100).IsRequired(false);
    builder.Ignore(x => x.IsDeleted);

    // soft delete: a deleted account stays for audit history but disappears from every query
    builder.HasQueryFilter(x => x.DeletedAt == null);

    builder.HasIndex(x => x.Username).IsUnique();
    builder.HasIndex(x => x.Email).IsUnique();
    builder.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("employee_id IS NOT NULL");
    builder.HasIndex(x => x.EmployeeCode).IsUnique().HasFilter("employee_code IS NOT NULL");
    builder.HasIndex(x => x.IsActive);
  }
}
