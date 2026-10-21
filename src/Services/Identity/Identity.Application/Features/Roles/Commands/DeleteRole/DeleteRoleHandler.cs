using Microsoft.EntityFrameworkCore;

public class DeleteRoleHandler(IApplicationDbContext context,
    IActivityRecorder activity)
  : ICommandHandler<DeleteRoleCommand, Result<DeleteRoleCommandResult>>
{
  public async Task<Result<DeleteRoleCommandResult>> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = RoleId.Of(command.Id);

    var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new RoleNotFoundException("This role was not found - it may have been deleted.");

    // the domain refuses to delete a system role
    role.EnsureDeletable();

    // the same liveness as everywhere else: a grant that ran out is not holding the role
    var holders = await context.UserRoles.AsNoTracking()
      .Where(ur => ur.RoleId == id && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now))
      .Join(context.Users.AsNoTracking(), ur => ur.UserId, u => u.Id, (ur, u) => u.DisplayName.Value)
      .OrderBy(n => n)
      .ToListAsync(cancellationToken);

    if (holders.Count > 0)
      return Result<DeleteRoleCommandResult>.Failure(
        $"{Describe(holders)} still {(holders.Count == 1 ? "has" : "have")} this role. Take it from them first (Users, filtered by this role), or switch the role off instead.");

    role.SoftDelete(now);
    await activity.RecordAsync(ActivityAction.RoleDeleted, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, null, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteRoleCommandResult>.Success(new DeleteRoleCommandResult(true));
  }

  private static string Describe(IReadOnlyList<string> names) => names.Count switch
  {
    1 => names[0],
    2 => $"{names[0]} and {names[1]}",
    3 => $"{names[0]}, {names[1]} and {names[2]}",
    _ => $"{names[0]}, {names[1]} and {names.Count - 2} others",
  };
}
