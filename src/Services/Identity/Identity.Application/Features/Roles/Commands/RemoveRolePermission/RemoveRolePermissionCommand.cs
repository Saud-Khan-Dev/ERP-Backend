using FluentValidation;

public sealed record RemoveRolePermissionCommandResult(bool IsSuccess);

public sealed record RemoveRolePermissionCommand(Guid RoleId, Guid PermissionId)
  : ICommand<Result<RemoveRolePermissionCommandResult>>;

public class RemoveRolePermissionCommandValidator : AbstractValidator<RemoveRolePermissionCommand>
{
  public RemoveRolePermissionCommandValidator()
  {
    RuleFor(x => x.RoleId).NotEmpty();
    RuleFor(x => x.PermissionId).NotEmpty();
  }
}
