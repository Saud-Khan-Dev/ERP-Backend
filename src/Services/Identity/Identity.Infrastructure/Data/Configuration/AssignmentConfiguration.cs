using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserRoleConfiguration : EntityConfiguration<UserRole, UserRoleId>
{
  public override void Configure(EntityTypeBuilder<UserRole> builder)
  {
    base.Configure(builder);

    builder.ToTable("user_roles");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id).HasConversion(id => id.Value, dbId => UserRoleId.Of(dbId));

    builder.Property(x => x.UserId)
      .HasConversion(id => id.Value, dbId => UserId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.RoleId)
      .HasConversion(id => id.Value, dbId => RoleId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AssignedAt).IsRequired();
    builder.Property(x => x.AssignedBy).IsRequired(false);
    builder.Property(x => x.ExpiresAt).IsRequired(false);
    builder.Property(x => x.RevokedAt).IsRequired(false);

    builder.HasIndex(x => x.UserId);
    builder.HasIndex(x => x.RoleId);
    builder.HasIndex(x => x.ExpiresAt);

    // only one *live* grant per (user, role) — revoked grants stay for the audit trail, and the
    // same role can be granted again later
    builder.HasIndex(x => new { x.UserId, x.RoleId })
      .IsUnique()
      .HasDatabaseName("ux_user_roles_live")
      .HasFilter("revoked_at IS NULL");

    builder.HasOne<User>()
      .WithMany()
      .HasForeignKey(x => x.UserId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Role>()
      .WithMany()
      .HasForeignKey(x => x.RoleId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);
  }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
  public void Configure(EntityTypeBuilder<RolePermission> builder)
  {
    builder.ToTable("role_permissions");

    // pure join table: the surrogate Id from the base class is not used
    builder.Ignore(x => x.Id);
    builder.HasKey(x => new { x.RoleId, x.PermissionId });

    builder.Property(x => x.RoleId)
      .HasConversion(id => id.Value, dbId => RoleId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.PermissionId)
      .HasConversion(id => id.Value, dbId => PermissionId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.AssignedAt).IsRequired();
    builder.Property(x => x.AssignedBy).IsRequired(false);

    builder.Property(x => x.CreatedAt).IsRequired(false);
    builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.LastModified).IsRequired(false);
    builder.Property(x => x.LastModifiedBy).HasMaxLength(100).IsRequired(false);

    builder.HasIndex(x => x.RoleId);
    builder.HasIndex(x => x.PermissionId);

    builder.HasOne<Role>()
      .WithMany()
      .HasForeignKey(x => x.RoleId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Permission>()
      .WithMany()
      .HasForeignKey(x => x.PermissionId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);
  }
}

public class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
  public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
  {
    builder.ToTable("user_permission_overrides");

    builder.Ignore(x => x.Id);
    // one row per (user, permission): ALLOW and DENY are mutually exclusive by construction
    builder.HasKey(x => new { x.UserId, x.PermissionId });

    builder.Property(x => x.UserId)
      .HasConversion(id => id.Value, dbId => UserId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.PermissionId)
      .HasConversion(id => id.Value, dbId => PermissionId.Of(dbId))
      .IsRequired();

    builder.Property(x => x.Effect).HasConversion<string>().HasMaxLength(10).IsRequired();
    builder.Property(x => x.GrantedAt).IsRequired();
    builder.Property(x => x.GrantedBy).IsRequired(false);
    builder.Property(x => x.ExpiresAt).IsRequired(false);
    builder.Property(x => x.Reason).HasMaxLength(500).IsRequired(false);

    builder.Property(x => x.CreatedAt).IsRequired(false);
    builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired(false);
    builder.Property(x => x.LastModified).IsRequired(false);
    builder.Property(x => x.LastModifiedBy).HasMaxLength(100).IsRequired(false);

    builder.HasIndex(x => x.UserId);
    builder.HasIndex(x => x.PermissionId);

    builder.HasOne<User>()
      .WithMany()
      .HasForeignKey(x => x.UserId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Permission>()
      .WithMany()
      .HasForeignKey(x => x.PermissionId)
      .IsRequired()
      .OnDelete(DeleteBehavior.Cascade);
  }
}
