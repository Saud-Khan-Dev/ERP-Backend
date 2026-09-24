using Microsoft.EntityFrameworkCore;

public class DeleteRoleHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteRoleCommand, Result<DeleteRoleCommandResult>>
{
  public async Task<Result<DeleteRoleCommandResult>> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
  {
    var now = DateTime.UtcNow;
    var id = RoleId.Of(command.Id);

    var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.Id} was not found.");

    // the domain refuses to delete a system role
    role.EnsureDeletable();

    var holders = await context.UserRoles.CountAsync(ur => ur.RoleId == id && ur.RevokedAt == null, cancellationToken);

    if (holders > 0)
      return Result<DeleteRoleCommandResult>.Failure(
        $"{holders} user(s) still hold this role. Remove it from them first, or deactivate the role instead.");

    role.SoftDelete(now);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteRoleCommandResult>.Success(new DeleteRoleCommandResult(true));
  }
}
