using Microsoft.EntityFrameworkCore;

public class UpdateRoleHandler(IApplicationDbContext context,
    IActivityRecorder activity)
  : ICommandHandler<UpdateRoleCommand, Result<UpdateRoleCommandResult>>
{
  public async Task<Result<UpdateRoleCommandResult>> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
  {
    var id = RoleId.Of(command.Id);
    var role = await context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
      ?? throw new RoleNotFoundException($"Role {command.Id} was not found.");

    var code = LookupCode.Of(command.Code);

    if (await context.Roles.AnyAsync(r => r.Id != id && r.Code == code, cancellationToken))
      return Result<UpdateRoleCommandResult>.Failure($"A role with code '{code.Value}' already exists.");

    var before = (Name: role.RoleName.Value, Code: role.Code.Value, role.Description, role.IsActive);

    // the domain refuses to rename or deactivate a system role
    role.Update(code, Name.Of(command.Name, 100), command.Description, command.IsActive);

    // a save that changed nothing is not a change
    var detail = ActivityDetail.Changes(
      ("Name", before.Name, role.RoleName.Value),
      ("Code", before.Code, role.Code.Value),
      ("Description", before.Description, role.Description),
      ("Active", ActivityDetail.YesNo(before.IsActive), ActivityDetail.YesNo(role.IsActive)));
    if (detail is not null)
      await activity.RecordAsync(ActivityAction.RoleUpdated, ActivityTargetType.Role, role.Id.Value, role.RoleName.Value, detail, cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateRoleCommandResult>.Success(new UpdateRoleCommandResult(true));
  }
}
