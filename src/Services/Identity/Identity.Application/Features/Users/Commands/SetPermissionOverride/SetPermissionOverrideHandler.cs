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

    guard.EnsureNotSelf(userId, "make exceptions on");

    if (!await context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
      throw new UserNotFoundException("This account was not found.");

    await guard.EnsureCanActOnAsync(userId, "make exceptions on", cancellationToken);

    var permission = await context.Permissions.AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == permissionId, cancellationToken)
      ?? throw new PermissionNotFoundException("This permission was not found.");

    if (!permission.IsActive)
      return Result<SetPermissionOverrideCommandResult>.Failure($"'{permission.PermissionName.Value}' is switched off.");

    // an Allow exception is a grant: nobody grants what they do not hold
    if (command.Effect == OverrideEffect.Allow)
      await guard.EnsureCanGrantAsync([permission.Id], "allow this", cancellationToken);

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

    // tokens issued before this change stop working at once
    await context.AccessChangedAsync(userId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<SetPermissionOverrideCommandResult>.Success(new SetPermissionOverrideCommandResult(true));
  }
}
