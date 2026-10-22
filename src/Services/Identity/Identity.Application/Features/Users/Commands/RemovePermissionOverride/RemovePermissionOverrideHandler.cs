using Microsoft.EntityFrameworkCore;

public class RemovePermissionOverrideHandler(IApplicationDbContext context, IdentityGuard guard,
    IActivityRecorder activity)
  : ICommandHandler<RemovePermissionOverrideCommand, Result<RemovePermissionOverrideCommandResult>>
{
  public async Task<Result<RemovePermissionOverrideCommandResult>> Handle(RemovePermissionOverrideCommand command, CancellationToken cancellationToken)
  {
    var userId = UserId.Of(command.UserId);
    var permissionId = PermissionId.Of(command.PermissionId);

    guard.EnsureNotSelf(userId, "make exceptions on");
    await guard.EnsureCanActOnAsync(userId, "make exceptions on", cancellationToken);

    var existing = await context.UserPermissionOverrides
        .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == permissionId, cancellationToken)
      ?? throw new DomainException("This person has no exception for that permission.");

    // taking a Deny away gives the permission back: only someone who holds it may
    if (existing.Effect == OverrideEffect.Deny)
      await guard.EnsureCanGrantAsync([permissionId], "lift this exception", cancellationToken);

    context.UserPermissionOverrides.Remove(existing);
    var targetName = await context.Users.IgnoreQueryFilters().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Username.Value).FirstOrDefaultAsync(cancellationToken);
    var code = await context.Permissions.AsNoTracking().Where(p => p.Id == permissionId).Select(p => p.Code).FirstOrDefaultAsync(cancellationToken);
    await activity.RecordAsync(ActivityAction.PermissionOverrideRemoved, ActivityTargetType.User, userId.Value, targetName,
      code is null ? null : $"{code.Value} \u00b7 {existing.Effect}", cancellationToken);

    // tokens issued before this change stop working at once
    await context.AccessChangedAsync(userId, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RemovePermissionOverrideCommandResult>.Success(new RemovePermissionOverrideCommandResult(true));
  }
}
