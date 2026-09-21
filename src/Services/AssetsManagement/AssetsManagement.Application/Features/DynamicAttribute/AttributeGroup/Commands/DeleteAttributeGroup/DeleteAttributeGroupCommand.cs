using FluentValidation;

public sealed record DeleteAttributeGroupCommandResult(bool IsSuccess);

public sealed record DeleteAttributeGroupCommand(Guid Id) : ICommand<Result<DeleteAttributeGroupCommandResult>>;

public class DeleteAttributeGroupCommandValidator : AbstractValidator<DeleteAttributeGroupCommand>
{
  public DeleteAttributeGroupCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
