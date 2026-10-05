using Microsoft.EntityFrameworkCore;

public class SetPermissionOverrideHandler(IApplicationDbContext context, IdentityGuard guard, ICurrentUser currentUser,
    IActivityRecorder activity)
  : ICommandHandler<SetPermissionOverrideCommand, Result<SetPermissionOverrideCommandResult>>
{
  public async Task<Result<SetPermissionOverrideCommandResult>> Handle(SetPermissionOverrideCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var userId = UserId.Of(command.UserId);
    var permissionId = PermissionId.Of(command.PermissionId);

    guard.EnsureNotSelf(userId, "change permissions");

    if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException($"User {command.UserId} was not found.");

    var permission = await context.Permissions.AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == permissionId, cancellationToken)
      ?? throw new PermissionNotFoundException($"Permission {command.PermissionId} was not found.");

    if (!permission.IsActive)
      return Result<SetPermissionOverrideCommandResult>.Failure($"Permission '{permission.Code.Value}' is inactive.");

    var existing = await context.UserPermissionOverrides
        .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == permissionId, cancellationToken);

    if (existing is null)
    {
      await context.UserPermissionOverrides.AddAsync(
        UserPermissionOverride.Create(userId, permissionId, command.Effect, currentUser.UserId,
          command.ExpiresAt, command.Reason, now), cancellationToken);
    }
    else
    {
      // one row per (user, permission): switching ALLOW to DENY updates in place
      existing.Update(command.Effect, command.ExpiresAt, command.Reason, now);
    }

    var targetName = await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    await activity.RecordAsync(ActivityAction.PermissionOverrideSet, ActivityTargetType.User, userId.Value, targetName,
      $"{permission.Code.Value} \u00b7 {command.Effect}", cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<SetPermissionOverrideCommandResult>.Success(new SetPermissionOverrideCommandResult(true));
  }
}
