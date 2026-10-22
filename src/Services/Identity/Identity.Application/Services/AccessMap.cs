using Microsoft.EntityFrameworkCore;

/// What every account may do, and WHY - built in one pass over the tables (not one query per person), for the access
/// reports, the Overview and the separation-of-duty checks. Same precedence as PermissionResolver: a live Deny exception
/// wins, then a live Allow, then any live role.
public sealed class AccessMap
{
  /// One permission an account holds and where it comes from ("Role · Tax clerk", "Exception · until 01 Nov 2026").
  public sealed record Grant(string Code, IReadOnlyList<string> Sources);

  public sealed record Account(
    User User,
    IReadOnlyList<(Role Role, UserRole Assignment)> Roles,
    IReadOnlyDictionary<string, Grant> Permissions,
    IReadOnlyList<string> Denied,
    IReadOnlyList<UserScope> Scopes);

  public IReadOnlyDictionary<UserId, Account> Accounts { get; }

  /// Code → the permission's plain name.
  public IReadOnlyDictionary<string, string> Names { get; }

  private AccessMap(IReadOnlyDictionary<UserId, Account> accounts, IReadOnlyDictionary<string, string> names)
  {
    Accounts = accounts;
    Names = names;
  }

  public static async Task<AccessMap> BuildAsync(IApplicationDbContext context, CancellationToken cancellationToken, bool includeDeleted = false)
  {
    var now = DateTime.UtcNow;
    var users = await (includeDeleted ? context.Users.IgnoreQueryFilters() : context.Users).AsNoTracking().ToListAsync(cancellationToken);
    var roles = await context.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, cancellationToken);
    var grants = await context.UserRoles.AsNoTracking().Where(ur => ur.RevokedAt == null).ToListAsync(cancellationToken);
    var permissions = await context.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id, cancellationToken);
    var rolePermissions = (await context.RolePermissions.AsNoTracking().ToListAsync(cancellationToken))
      .GroupBy(rp => rp.RoleId)
      .ToDictionary(g => g.Key, g => g.Select(rp => permissions[rp.PermissionId]).Where(p => p.IsActive).ToList());
    var overrides = (await context.UserPermissionOverrides.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(o => o.UserId);
    var scopes = (await context.UserScopes.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(s => s.UserId);
    var byUser = grants.ToLookup(g => g.UserId);

    var accounts = new Dictionary<UserId, Account>();
    foreach (var user in users)
    {
      var live = byUser[user.Id]
        .Where(g => roles.ContainsKey(g.RoleId))
        .Select(g => (Role: roles[g.RoleId], Assignment: g))
        .ToList();
      var sources = new Dictionary<string, List<string>>(StringComparer.Ordinal);
      foreach (var (role, assignment) in live.Where(x => x.Assignment.IsLive(now) && x.Role.IsActive && !x.Role.IsDeleted))
        foreach (var permission in rolePermissions.GetValueOrDefault(role.Id) ?? [])
          Add(sources, permission.Code.Value, assignment.ExpiresAt is { } end ? $"Role · {role.RoleName.Value} (until {end:dd MMM yyyy})" : $"Role · {role.RoleName.Value}");

      var denied = new List<string>();
      foreach (var o in overrides[user.Id].Where(o => o.IsLive(now) && permissions.TryGetValue(o.PermissionId, out var p) && p.IsActive))
      {
        var code = permissions[o.PermissionId].Code.Value;
        if (o.Effect == OverrideEffect.Deny)
          denied.Add(code);
        else
          Add(sources, code, o.ExpiresAt is { } end ? $"Exception · until {end:dd MMM yyyy}" : "Exception");
      }
      foreach (var code in denied)
        sources.Remove(code);

      accounts[user.Id] = new Account(
        user,
        live,
        sources.ToDictionary(kv => kv.Key, kv => new Grant(kv.Key, kv.Value), StringComparer.Ordinal),
        denied,
        scopes[user.Id].ToList());
    }

    return new AccessMap(accounts, permissions.Values.ToDictionary(p => p.Code.Value, p => p.PermissionName.Value, StringComparer.Ordinal));
  }

  /// The conflicting pairs (PermissionCatalog.Conflicts) a set of permissions holds together.
  public static IReadOnlyList<PermissionCatalog.Conflict> ConflictsIn(IEnumerable<string> codes)
  {
    var held = codes.ToHashSet(StringComparer.Ordinal);
    return PermissionCatalog.Conflicts.Where(c => held.Contains(c.First) && held.Contains(c.Second)).ToList();
  }

  public bool IsSuperAdmin(Account account) =>
    account.Roles.Any(r => r.Role.IsSuperAdmin && r.Assignment.IsLive(DateTime.UtcNow));

  private static void Add(Dictionary<string, List<string>> sources, string code, string source)
  {
    if (!sources.TryGetValue(code, out var list))
      sources[code] = list = [];
    if (!list.Contains(source))
      list.Add(source);
  }
}
