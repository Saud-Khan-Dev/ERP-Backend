using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// Brings the database up to the state the application expects at startup:
///
///   1. permission modules and permissions, from the shared PermissionCatalog
///   2. the protected SUPER_ADMIN role, holding every permission
///   3. the first Super Admin account, if no Super Admin exists yet
///   4. the security settings, from configuration, the first time
///
/// Idempotent: safe to run on every start. Steps 1 and 2 also *reconcile* — a permission added to
/// the catalogue in code appears in the database and on SUPER_ADMIN without a manual migration.
public sealed class IdentitySeeder(
    ApplicationDbContext context,
    IPasswordHasher passwordHasher,
    IPasswordGenerator passwordGenerator,
    IOptions<SeedOptions> seedOptions,
    IOptions<SecurityOptions> securityOptions,
    IOptions<JwtOptions> jwtOptions,
    IHostEnvironment environment,
    ILogger<IdentitySeeder> logger)
{
  private readonly SeedOptions _seed = seedOptions.Value;
  private readonly SecurityOptions _security = securityOptions.Value;
  private readonly JwtOptions _jwt = jwtOptions.Value;

  public async Task SeedAsync(CancellationToken cancellationToken = default)
  {
    if (!_seed.Enabled)
    {
      logger.LogInformation("Identity seeding is disabled.");
      return;
    }

    var moduleIds = await SeedModulesAsync(cancellationToken);
    var added = await SeedPermissionsAsync(moduleIds, cancellationToken);
    await GrantInheritedAsync(added, cancellationToken);
    var superAdminRole = await SeedSuperAdminRoleAsync(cancellationToken);
    await SeedSuperAdminUserAsync(superAdminRole, cancellationToken);
    await SeedSecuritySettingsAsync(cancellationToken);
  }

  /// Writes the one security-settings row the first time, from the values in configuration, so the
  /// running policy is unchanged until an administrator edits it in the app.
  private async Task SeedSecuritySettingsAsync(CancellationToken cancellationToken)
  {
    var id = SecuritySettingsId.Of(SecuritySettings.SingletonId);

    if (await context.SecuritySettings.AnyAsync(s => s.Id == id, cancellationToken))
      return;

    var settings = SecuritySettings.Create(
      id,
      _security.PasswordMinimumLength, _security.PasswordRequireUppercase, _security.PasswordRequireLowercase,
      _security.PasswordRequireDigit, _security.PasswordRequireNonAlphanumeric,
      _security.MaxFailedLoginAttempts, _security.LockoutMinutes,
      _jwt.AccessTokenMinutes, _jwt.RefreshTokenDays);

    await context.SecuritySettings.AddAsync(settings, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    logger.LogInformation("Seeded the security settings.");
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

  /// Adds the catalogue's new permissions, keeps every name in step with it, and switches off a permission the catalogue
  /// no longer has (it stays in the table so history reads, but no role grants it any more). Returns the codes added.
  private async Task<List<string>> SeedPermissionsAsync(Dictionary<string, PermissionModuleId> moduleIds, CancellationToken cancellationToken)
  {
    var existing = await context.Permissions.ToDictionaryAsync(p => p.Code.Value, StringComparer.Ordinal, cancellationToken);
    var catalogue = PermissionCatalog.All.ToDictionary(p => p.Code, StringComparer.Ordinal);
    var added = new List<string>();
    var changed = 0;

    foreach (var definition in PermissionCatalog.All)
    {
      if (existing.TryGetValue(definition.Code, out var known))
      {
        if (known.PermissionName.Value != definition.Name || !known.IsActive)
        {
          known.Update(Name.Of(definition.Name), known.Description, isActive: true);
          changed++;
        }
        continue;
      }

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

      added.Add(definition.Code);
    }

    foreach (var gone in existing.Values.Where(p => p.IsActive && !catalogue.ContainsKey(p.Code.Value)))
    {
      gone.Update(gone.PermissionName, gone.Description, isActive: false);
      changed++;
    }

    if (added.Count > 0 || changed > 0)
    {
      await context.SaveChangesAsync(cancellationToken);
      logger.LogInformation("Seeded {Added} permission(s), brought {Changed} in step with the catalogue.", added.Count, changed);
    }
    return added;
  }

  /// A permission split off an older one goes to every role that held the older one, once, when it is first created -
  /// nobody's work stops; an administrator then takes it away where it is not wanted. Written to the activity trail as
  /// the system's own change.
  private async Task GrantInheritedAsync(List<string> added, CancellationToken cancellationToken)
  {
    var inherited = added.Where(code => PermissionCatalog.InheritedFrom.ContainsKey(code)).ToList();
    if (inherited.Count == 0)
      return;

    var permissions = await context.Permissions.ToDictionaryAsync(p => p.Code.Value, p => p.Id, StringComparer.Ordinal, cancellationToken);
    var superAdmin = LookupCode.Of(Role.SuperAdminCode);
    var roles = await context.Roles.IgnoreQueryFilters().Where(r => r.Code != superAdmin).ToListAsync(cancellationToken);
    var held = await context.RolePermissions.Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(cancellationToken);
    var now = DateTime.UtcNow;
    var lines = new List<string>();
    var touched = new HashSet<RoleId>();

    foreach (var code in inherited)
    {
      var sources = PermissionCatalog.InheritedFrom[code].Where(permissions.ContainsKey).Select(c => permissions[c]).ToHashSet();
      foreach (var role in roles)
      {
        var holds = held.Any(h => h.RoleId == role.Id && sources.Contains(h.PermissionId));
        var already = held.Any(h => h.RoleId == role.Id && h.PermissionId == permissions[code]);
        if (!holds || already)
          continue;
        await context.RolePermissions.AddAsync(RolePermission.Create(role.Id, permissions[code], assignedBy: null, now), cancellationToken);
        lines.Add($"{role.RoleName.Value}: {code}");
        touched.Add(role.Id);
      }
    }

    if (lines.Count == 0)
      return;

    foreach (var roleId in touched)
    {
      var holderIds = await context.UserRoles.Where(ur => ur.RoleId == roleId && ur.RevokedAt == null).Select(ur => ur.UserId).ToListAsync(cancellationToken);
      foreach (var holder in await context.Users.Where(u => holderIds.Contains(u.Id)).ToListAsync(cancellationToken))
        holder.AccessChanged();
    }

    await context.AdminActivities.AddAsync(AdminActivity.Record(
      null, "System · Startup", ActivityAction.PermissionsSeeded, ActivityTargetType.System, null, "New permissions",
      "Split from what these roles already did: " + string.Join("; ", lines), null, null, now), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    logger.LogInformation("Gave {Count} new permission(s) to the roles that did their acts before: {Lines}", lines.Count, string.Join("; ", lines));
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
      // the holders' tokens are re-issued with the new permissions
      var holderIds = await context.UserRoles.Where(ur => ur.RoleId == role.Id && ur.RevokedAt == null).Select(ur => ur.UserId).ToListAsync(cancellationToken);
      foreach (var holder in await context.Users.Where(u => holderIds.Contains(u.Id)).ToListAsync(cancellationToken))
        holder.AccessChanged();
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
    user.AccessChanged();

    await context.AdminActivities.AddAsync(AdminActivity.Record(
      null, "System · Startup", ActivityAction.SuperAdminSeeded, ActivityTargetType.User, user.Id.Value, user.DisplayName.Value,
      "No Super Administrator was left, so the first one was set up at startup", null, null, now), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
  }
}
