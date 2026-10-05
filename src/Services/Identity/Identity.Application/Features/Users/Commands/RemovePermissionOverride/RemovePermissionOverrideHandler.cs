using Microsoft.EntityFrameworkCore;

public class RemovePermissionOverrideHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
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
    var targetName = await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    await activity.RecordAsync(ActivityAction.PermissionOverrideRemoved, ActivityTargetType.User, userId.Value, targetName, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<RemovePermissionOverrideCommandResult>.Success(new RemovePermissionOverrideCommandResult(true));
  }
}
