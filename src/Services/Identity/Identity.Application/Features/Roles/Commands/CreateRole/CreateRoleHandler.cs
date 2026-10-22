using Microsoft.EntityFrameworkCore;

public class CreateRoleHandler(IApplicationDbContext context, ICurrentUser currentUser, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<CreateRoleCommand, Result<CreateRoleCommandResult>>
{
  public async Task<Result<CreateRoleCommandResult>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var input = command.Role;
    var name = input.Name.Trim();

    if (await context.Roles.AnyAsync(r => r.RoleName == Name.Of(name, 100), cancellationToken))
      return Result<CreateRoleCommandResult>.Failure($"A role called {name} already exists - open it instead, or choose another name.");

    LookupCode code;
    if (string.IsNullOrWhiteSpace(input.Code))
      code = await RoleCodes.FreeCodeForAsync(context, name, cancellationToken);
    else
    {
      code = LookupCode.Of(input.Code);
      if (await context.Roles.IgnoreQueryFilters().AnyAsync(r => r.Code == code, cancellationToken))
        return Result<CreateRoleCommandResult>.Failure($"A role with the code {code.Value} already exists (or existed and was deleted) - choose another code.");
    }

    if (code.Value == Role.SuperAdminCode)
      return Result<CreateRoleCommandResult>.Failure("Super Administrator is a built-in role and cannot be made again.");

    // roles created through the API are never system roles
    var role = Role.Create(RoleId.Of(Guid.NewGuid()), code, Name.Of(name, 100), input.Description, isSystem: false);

    await context.Roles.AddAsync(role, cancellationToken);

    var permissionIds = await ResolvePermissionIdsAsync(input.PermissionIds, cancellationToken);
    if (permissionIds.Count > 0)
    {
      // deciding what a role can do is the access-giving right, not the role-creating one
      guard.EnsurePermission(PermissionCatalog.Permissions.Assign, "decide what a new role can do (that needs the right to change access)");
      await guard.EnsureCanGrantAsync(permissionIds, "give the new role this access", cancellationToken);
      var codes = await context.Permissions.AsNoTracking().Where(p => permissionIds.Contains(p.Id)).Select(p => p.Code.Value).ToListAsync(cancellationToken);
      await ConflictCheck.EnsureAcceptedAsync(activity, codes, input.ConflictReason, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, cancellationToken);
    }
    foreach (var permissionId in permissionIds)
      await context.RolePermissions.AddAsync(
        RolePermission.Create(role.Id, permissionId, currentUser.UserId, now), cancellationToken);

    await activity.RecordAsync(ActivityAction.RoleCreated, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value,
      await RolePermissionDetail.DescribeAsync(context, permissionIds, [], cancellationToken), cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateRoleCommandResult>.Success(new CreateRoleCommandResult(role.Id.Value));
  }

  private async Task<List<PermissionId>> ResolvePermissionIdsAsync(IReadOnlyList<Guid>? permissionIds, CancellationToken cancellationToken)
  {
    if (permissionIds is null || permissionIds.Count == 0)
      return new List<PermissionId>();

    var ids = permissionIds.Distinct().Select(PermissionId.Of).ToList();
    var found = await context.Permissions.CountAsync(p => ids.Contains(p.Id), cancellationToken);

    if (found != ids.Count)
      throw new PermissionNotFoundException("One or more of the supplied permissions was not found.");

    return ids;
  }
}

/// Role codes are made by the server from the role's name (DEPUTY_DIRECTOR_TAX), never typed: a code stays the same when
/// the role is renamed, and one a deleted role used is never handed out again.
public static class RoleCodes
{
  public static async Task<LookupCode> FreeCodeForAsync(IApplicationDbContext context, string name, CancellationToken cancellationToken)
  {
    var stem = new string(name.ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
    stem = System.Text.RegularExpressions.Regex.Replace(stem, "_+", "_").Trim('_');
    if (stem.Length == 0 || !char.IsAsciiLetterOrDigit(stem[0]))
      stem = "ROLE";
    if (stem.Length > 40)
      stem = stem[..40].TrimEnd('_');

    for (var n = 1; n < 1000; n++)
    {
      var candidate = LookupCode.Of(n == 1 ? stem : $"{stem}_{n}");
      if (candidate.Value == Role.SuperAdminCode)
        continue;
      if (!await context.Roles.IgnoreQueryFilters().AnyAsync(r => r.Code == candidate, cancellationToken))
        return candidate;
    }
    throw new DomainException("Could not find a free code for this role - choose another name.");
  }
}
