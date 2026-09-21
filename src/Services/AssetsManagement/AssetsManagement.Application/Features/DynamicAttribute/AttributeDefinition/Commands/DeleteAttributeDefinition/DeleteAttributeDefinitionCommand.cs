using FluentValidation;

public sealed record DeleteAttributeDefinitionCommandResult(bool IsSuccess);

public sealed record DeleteAttributeDefinitionCommand(Guid Id) : ICommand<Result<DeleteAttributeDefinitionCommandResult>>;

public class DeleteAttributeDefinitionCommandValidator : AbstractValidator<DeleteAttributeDefinitionCommand>
{
  public DeleteAttributeDefinitionCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
