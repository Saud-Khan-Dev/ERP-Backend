using FluentValidation;

public sealed record UpdateAttributeDefinitionCommandResult(bool IsSuccess);

public sealed record UpdateAttributeDefinitionCommand(Guid Id, AttributeDefinitionInput Definition) : ICommand<Result<UpdateAttributeDefinitionCommandResult>>;

public class UpdateAttributeDefinitionCommandValidator : AbstractValidator<UpdateAttributeDefinitionCommand>
{
  public UpdateAttributeDefinitionCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Definition).NotNull().SetValidator(new AttributeDefinitionInputValidator());
  }
}
