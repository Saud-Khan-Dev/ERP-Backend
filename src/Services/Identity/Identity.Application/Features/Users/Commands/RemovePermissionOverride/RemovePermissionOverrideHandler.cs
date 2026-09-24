using Microsoft.EntityFrameworkCore;

public class RemovePermissionOverrideHandler(IApplicationDbContext context, IdentityGuard guard)
  : ICommandHandler<RemovePermissionOverrideCommand, Result<RemovePermissionOverrideCommandResult>>
{
  public async Task<Result<RemovePermissionOverrideCommandResult>> Handle(RemovePermissionOverrideCommand command, CancellationToken cancellationToken)
  {
    var userId = UserId.Of(command.UserId);
    var permissionId = PermissionId.Of(command.PermissionId);

    guard.EnsureNotSelf(userId, "change permissions");

    var existing = await context.UserPermissionOverrides
        .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == permissionId, cancellationToken)
      ?? throw new PermissionNotFoundException("That override does not exist for this user.");

    context.UserPermissionOverrides.Remove(existing);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RemovePermissionOverrideCommandResult>.Success(new RemovePermissionOverrideCommandResult(true));
  }
}
