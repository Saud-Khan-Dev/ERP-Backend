using FluentValidation;

public sealed record AttributeValueInput(Guid AttributeDefinitionId, string? Value);

public sealed record SavePropertyAttributesCommandResult(int SavedCount);

/// Saves the custom-field form of a property; each value is checked against its field's data type.
/// A null value clears the field (refused for required fields).
public sealed record SavePropertyAttributesCommand(Guid PropertyId, IReadOnlyList<AttributeValueInput> Values) : ICommand<Result<SavePropertyAttributesCommandResult>>;

public class AttributeValueInputValidator : AbstractValidator<AttributeValueInput>
{
  public AttributeValueInputValidator()
  {
    RuleFor(x => x.AttributeDefinitionId).NotEmpty();
  }
}

public class SavePropertyAttributesCommandValidator : AbstractValidator<SavePropertyAttributesCommand>
{
  public SavePropertyAttributesCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Values).NotEmpty().WithMessage("At least one value is required.");
    RuleForEach(x => x.Values).SetValidator(new AttributeValueInputValidator());
  }
}
