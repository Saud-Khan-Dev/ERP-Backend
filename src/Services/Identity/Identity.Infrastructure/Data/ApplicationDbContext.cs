using System.Reflection;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
  /// All services share one ERP database; this service owns the "auth" schema.
  public const string Schema = "auth";

  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

  // ---- accounts ----
  public DbSet<User> Users => Set<User>();
  public DbSet<Session> Sessions => Set<Session>();
  public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
  public DbSet<EmployeeCodeTemplate> EmployeeCodeTemplates => Set<EmployeeCodeTemplate>();

  // ---- authorization ----
  public DbSet<Role> Roles => Set<Role>();
  public DbSet<PermissionModule> PermissionModules => Set<PermissionModule>();
  public DbSet<Permission> Permissions => Set<Permission>();
  public DbSet<UserRole> UserRoles => Set<UserRole>();
  public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
  public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

  protected override void OnModelCreating(ModelBuilder builder)
  {
    builder.HasDefaultSchema(Schema);
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    base.OnModelCreating(builder);
  }
}
