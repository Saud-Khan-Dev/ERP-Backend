using FluentValidation;

public sealed record RemoveRoleFromUserCommandResult(bool IsSuccess);

public sealed record RemoveRoleFromUserCommand(Guid UserId, Guid RoleId)
  : ICommand<Result<RemoveRoleFromUserCommandResult>>;

public class RemoveRoleFromUserCommandValidator : AbstractValidator<RemoveRoleFromUserCommand>
{
  public RemoveRoleFromUserCommandValidator()
  {
    RuleFor(x => x.UserId).NotEmpty();
    RuleFor(x => x.RoleId).NotEmpty();
  }
}
