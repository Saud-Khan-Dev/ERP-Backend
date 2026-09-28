using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// Brings the database up to the state the application expects at startup:
///
///   1. permission modules and permissions, from the shared PermissionCatalog
///   2. the protected SUPER_ADMIN role, holding every permission
///   3. the first Super Admin account, if no Super Admin exists yet
///   4. the default employee code template (EMP-###), if none exists yet
///
/// Idempotent: safe to run on every start. Steps 1 and 2 also *reconcile* — a permission added to
/// the catalogue in code appears in the database and on SUPER_ADMIN without a manual migration.
public sealed class IdentitySeeder(
    ApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    IOptions<SeedOptions> seedOptions,
    IOptions<SecurityOptions> securityOptions,
    IHostEnvironment environment,
    ILogger<IdentitySeeder> logger)
{
  private readonly SeedOptions _seed = seedOptions.Value;

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    if (!_seed.Enabled)
    {
      logger.LogInformation("Identity seeding is disabled.");
      return;
    }

    var moduleIds = await SeedModulesAsync(cancellationToken);
    await SeedPermissionsAsync(moduleIds, cancellationToken);
    var superAdminRole = await SeedSuperAdminRoleAsync(cancellationToken);
    await SeedSuperAdminUserAsync(superAdminRole, cancellationToken);
    await SeedEmployeeCodeTemplateAsync(cancellationToken);
  }

  /// Only ever creates the default; an administrator's edits to the template are never overwritten.
  private async Task SeedEmployeeCodeTemplateAsync(CancellationToken cancellationToken)
  {
    var id = EmployeeCodeTemplateId.Of(EmployeeCodeTemplate.SingletonId);

    if (await context.EmployeeCodeTemplates.AnyAsync(t => t.Id == id, cancellationToken))
      return;

    var template = EmployeeCodeTemplate.CreateDefault();
    await context.EmployeeCodeTemplates.AddAsync(template, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    logger.LogInformation("Seeded the employee code template {Pattern}; the first code will be {Code}.",
      template.Pattern, template.NextCode.Value);
  }

  private async Task<Dictionary<string, PermissionModuleId>> SeedModulesAsync(CancellationToken cancellationToken)
  {
    var existing = await context.PermissionModules.ToDictionaryAsync(m => m.Code.Value, cancellationToken);
    var added = 0;

    foreach (var definition in PermissionCatalog.AllModules)
    {
      if (existing.ContainsKey(definition.Code))
        continue;

      var module = PermissionModule.Create(
        PermissionModuleId.Of(Guid.NewGuid()),
        LookupCode.Of(definition.Code),
        Name.Of(definition.Name, 100));

      await context.PermissionModules.AddAsync(module, cancellationToken);
      existing[definition.Code] = module;
      added++;
    }

    if (added > 0)
    {
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation("Seeded {Count} permission module(s).", added);
    }

    return existing.ToDictionary(kv => kv.Key, kv => kv.Value.Id);
  }

  private async Task SeedPermissionsAsync(Dictionary<string, PermissionModuleId> moduleIds, CancellationToken cancellationToken)
  {
    var existing = await context.Permissions.Select(p => p.Code.Value).ToListAsync(cancellationToken);
    var known = existing.ToHashSet(StringComparer.Ordinal);
    var added = 0;

    foreach (var definition in PermissionCatalog.All)
    {
      if (known.Contains(definition.Code))
        continue;

      if (!moduleIds.TryGetValue(definition.Module, out var moduleId))
      {
        logger.LogWarning("Permission {Code} references unknown module {Module}; skipped.", definition.Code, definition.Module);
        continue;
      }

      if (!Enum.TryParse<PermissionAction>(definition.Action, ignoreCase: true, out var action))
      {
        logger.LogWarning("Permission {Code} has unknown action {Action}; skipped.", definition.Code, definition.Action);
        continue;
      }

      await context.Permissions.AddAsync(Permission.Create(
        PermissionId.Of(Guid.NewGuid()),
        PermissionCode.Of(definition.Code),
        moduleId,
        action,
        Name.Of(definition.Name),
        description: null), cancellationToken);

      added++;
    }

    if (added > 0)
    {
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation("Seeded {Count} permission(s).", added);
    }
  }

  private async Task<Role> SeedSuperAdminRoleAsync(CancellationToken cancellationToken)
  {
    var code = LookupCode.Of(Role.SuperAdminCode);

    var role = await context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);

    if (role is null)
    {
      role = Role.Create(
        RoleId.Of(Guid.NewGuid()),
        code,
        Name.Of("Super Administrator", 100),
        "Full administrative access. Built in and protected from deletion.",
        isSystem: true);

      await context.Roles.AddAsync(role, cancellationToken);
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation("Seeded the {Role} role.", Role.SuperAdminCode);
    }

    // Super Admin always holds every permission, including ones added to the catalogue later
    var allPermissionIds = await context.Permissions.Select(p => p.Id).ToListAsync(cancellationToken);

    var held = await context.RolePermissions
        .Where(rp => rp.RoleId == role.Id)
        .Select(rp => rp.PermissionId)
        .ToListAsync(cancellationToken);

    var missing = allPermissionIds.Except(held).ToList();

    foreach (var permissionId in missing)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(role.Id, permissionId, assignedBy: null, DateTime.UtcNow), cancellationToken);

    if (missing.Count > 0)
    {
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation("Granted {Count} new permission(s) to {Role}.", missing.Count, Role.SuperAdminCode);
    }

    return role;
  }

  private async Task SeedSuperAdminUserAsync(Role superAdminRole, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;

    var alreadyExists = await context.UserRoles
        .AnyAsync(ur => ur.RoleId == superAdminRole.Id && ur.RevokedAt == null, cancellationToken);

    if (alreadyExists)
      return;

    var username = Username.Of(_seed.SuperAdminUsername);

    var user = await context.Users.IgnoreQueryFilters()
        .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    // A password from configuration is a developer convenience. Outside Development it is ignored
    // and a random one is generated instead, so the credentials committed in seed.json can never
    // become a real login.
    var configuredPassword = _seed.SuperAdminPassword;
    var isDevelopment = environment.IsDevelopment();

    if (!string.IsNullOrWhiteSpace(configuredPassword) && !isDevelopment)
    {
      logger.LogWarning(
        "A fixed {Role} password is configured but the environment is {Environment}; ignoring it and generating one instead.",
        Role.SuperAdminCode, environment.EnvironmentName);

      configuredPassword = null;
    }

    var generated = string.IsNullOrWhiteSpace(configuredPassword);

    if (user is null)
    {
      var password = generated
        ? passwordGenerator.Generate(securityOptions.Value.PasswordPolicy)
        : configuredPassword!;

      PasswordPolicy.Validate(password, securityOptions.Value.PasswordPolicy);

      user = User.Create(
        UserId.Of(Guid.NewGuid()),
        username,
        EmailAddress.Of(_seed.SuperAdminEmail),
        Name.Of(_seed.SuperAdminDisplayName),
        PasswordHash.Of(passwordHasher.Hash(password)),
        employeeId: null,
        employeeCode: null,
        // a generated bootstrap password must always be replaced on first use
        mustChangePassword: generated || _seed.SuperAdminMustChangePassword,
        now);

      await context.Users.AddAsync(user, cancellationToken);

      if (generated)
        logger.LogWarning(
          "Created the first {Role} '{Username}' with a generated password: {Password} — sign in and change it now.",
          Role.SuperAdminCode, username.Value, password);
      else
        logger.LogWarning(
          "Created the first {Role} '{Username}' with the shared development password from seed.json. Development only.",
          Role.SuperAdminCode, username.Value);
    }

    await context.UserRoles.AddAsync(
      UserRole.Create(user.Id, superAdminRole.Id, assignedBy: null, expiresAt: null, now), cancellationToken);

    await context.SaveChangesAsync(cancellationToken);
  }
}
