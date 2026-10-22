using Microsoft.EntityFrameworkCore;

/// Two guard rails make this safe: you cannot change your own roles, and you cannot hand out
/// SUPER_ADMIN unless you already hold it.
public class AssignRoleToUserHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser,
    IActivityRecorder activity)
  : ICommandHandler<AssignRoleToUserCommand, Result<AssignRoleToUserCommandResult>>
{
  public async Task<Result<AssignRoleToUserCommandResult>> Handle(AssignRoleToUserCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(command.UserId);
    var roleId = RoleId.Of(command.RoleId);

    guard.EnsureNotSelf(userId, "change the roles of");

    if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException("This account was not found.");

    await guard.EnsureCanActOnAsync(userId, "change the roles of", cancellationToken);

    var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException("This role was not found.");

    guard.EnsureCanAdministerRole(role);
    await guard.EnsureCanGrantRolesAsync([role.Id], $"give the role {role.RoleName.Value}", cancellationToken);

    if (!role.IsActive)
      return Result<AssignRoleToUserCommandResult>.Failure($"The role {role.RoleName.Value} is switched off and cannot be given.");

    // a grant that ran out is ended first, so the role can be given again (one live grant per role)
    var grants = await context.UserRoles.Where(ur => ur.UserId == userId && ur.RoleId == roleId && ur.RevokedAt == null).ToListAsync(cancellationToken);
    foreach (var expired in grants.Where(g => g.ExpiresAt is { } end && end <= now))
      expired.Revoke(now);

    if (grants.Any(g => g.ExpiresAt is null || g.ExpiresAt > now))
      return Result<AssignRoleToUserCommandResult>.Failure($"This person already has the role {role.RoleName.Value}.");

    var assignment = UserRole.Create(userId, roleId, currentUser.UserId, command.ExpiresAt, now);

    await context.UserRoles.AddAsync(assignment, cancellationToken);
    var targetName = await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    await activity.RecordAsync(ActivityAction.RoleAssigned, ActivityTargetType.User, userId.Value, targetName, role.RoleName.Value, cancellationToken);

    // tokens issued before this change stop working at once
    await context.AccessChangedAsync(userId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<AssignRoleToUserCommandResult>.Success(new AssignRoleToUserCommandResult(assignment.Id.Value));
  }
}
