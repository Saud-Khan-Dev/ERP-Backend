using Microsoft.EntityFrameworkCore;

public interface IApplicationDbContext
{
  // ---- accounts ----
  DbSet<User> Users { get; }
  DbSet<Session> Sessions { get; }
  DbSet<LoginAttempt> LoginAttempts { get; }
  DbSet<AdminActivity> AdminActivities { get; }
  DbSet<EmployeeCodeTemplate> EmployeeCodeTemplates { get; }
  DbSet<SecuritySettings> SecuritySettings { get; }

  // ---- authorization ----
  DbSet<Role> Roles { get; }
  DbSet<PermissionModule> PermissionModules { get; }
  DbSet<Permission> Permissions { get; }
  DbSet<UserRole> UserRoles { get; }
  DbSet<RolePermission> RolePermissions { get; }
  DbSet<UserPermissionOverride> UserPermissionOverrides { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
