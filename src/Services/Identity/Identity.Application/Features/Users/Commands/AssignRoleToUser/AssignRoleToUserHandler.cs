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

    guard.EnsureNotSelf(userId, "change roles");

    if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException($"User {command.UserId} was not found.");

    var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.RoleId} was not found.");

    guard.EnsureCanAdministerRole(role);

    if (!role.IsActive)
      return Result<AssignRoleToUserCommandResult>.Failure($"Role '{role.Code.Value}' is inactive and cannot be assigned.");

    var alreadyGranted = await context.UserRoles.AnyAsync(
      ur => ur.UserId == userId && ur.RoleId == roleId && ur.RevokedAt == null, cancellationToken);

    if (alreadyGranted)
      return Result<AssignRoleToUserCommandResult>.Failure($"This user already holds the '{role.Code.Value}' role.");

    var assignment = UserRole.Create(userId, roleId, currentUser.UserId, command.ExpiresAt, now);

    await context.UserRoles.AddAsync(assignment, cancellationToken);
    var targetName = await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    await activity.RecordAsync(ActivityAction.RoleAssigned, ActivityTargetType.User, userId.Value, targetName, role.RoleName.Value, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<AssignRoleToUserCommandResult>.Success(new AssignRoleToUserCommandResult(assignment.Id.Value));
  }
}
