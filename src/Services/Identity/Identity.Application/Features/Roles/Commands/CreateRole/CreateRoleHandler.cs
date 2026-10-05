using Microsoft.EntityFrameworkCore;

public class CreateRoleHandler(IApplicationDbContext context, ICurrentUser currentUser,
    IActivityRecorder activity)
  : ICommandHandler<CreateRoleCommand, Result<CreateRoleCommandResult>>
{
  public async Task<Result<CreateRoleCommandResult>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var input = command.Role;
    var code = LookupCode.Of(input.Code);

    if (code.Value == Role.SuperAdminCode)
      return Result<CreateRoleCommandResult>.Failure($"'{Role.SuperAdminCode}' is a built-in role and cannot be recreated.");

    if (await context.Roles.AnyAsync(r => r.Code == code, cancellationToken))
      return Result<CreateRoleCommandResult>.Failure($"A role with code '{code.Value}' already exists.");

    // roles created through the API are never system roles
    var role = Role.Create(RoleId.Of(Guid.NewGuid()), code, Name.Of(input.Name, 100), input.Description, isSystem: false);

    await context.Roles.AddAsync(role, cancellationToken);

    foreach (var permissionId in await ResolvePermissionIdsAsync(input.PermissionIds, cancellationToken))
      await context.RolePermissions.AddAsync(
        RolePermission.Create(role.Id, permissionId, currentUser.UserId, now), cancellationToken);

    await activity.RecordAsync(ActivityAction.RoleCreated, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, null, cancellationToken);

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
