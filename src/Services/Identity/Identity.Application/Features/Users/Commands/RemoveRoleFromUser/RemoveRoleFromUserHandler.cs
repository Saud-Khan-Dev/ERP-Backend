using Microsoft.EntityFrameworkCore;

/// Revokes rather than deletes, so the grant and its removal both stay in the audit trail.
public class RemoveRoleFromUserHandler(IApplicationDbContext context, IdentityGuard guard)
  : ICommandHandler<RemoveRoleFromUserCommand, Result<RemoveRoleFromUserCommandResult>>
{
  public async Task<Result<RemoveRoleFromUserCommandResult>> Handle(RemoveRoleFromUserCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(command.UserId);
    var roleId = RoleId.Of(command.RoleId);

    guard.EnsureNotSelf(userId, "change roles");

    var role = await context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.RoleId} was not found.");

    guard.EnsureCanAdministerRole(role);

    if (role.IsSuperAdmin)
      await guard.EnsureNotLastSuperAdminAsync(userId, "removing this role", cancellationToken);

    var assignment = await context.UserRoles
        .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.RevokedAt == null, cancellationToken)
      ?? throw new RoleNotFoundException("This user does not hold that role.");

    assignment.Revoke(now);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RemoveRoleFromUserCommandResult>.Success(new RemoveRoleFromUserCommandResult(true));
  }
}
