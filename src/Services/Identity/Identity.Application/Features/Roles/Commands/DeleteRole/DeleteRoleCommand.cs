using FluentValidation;

public sealed record DeleteRoleCommandResult(bool IsSuccess);

public sealed record DeleteRoleCommand(Guid Id) : ICommand<Result<DeleteRoleCommandResult>>;

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
  public DeleteRoleCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
