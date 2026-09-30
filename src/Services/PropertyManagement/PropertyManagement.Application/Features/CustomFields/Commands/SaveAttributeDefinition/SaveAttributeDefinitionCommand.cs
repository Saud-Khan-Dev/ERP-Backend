using FluentValidation;

/// A custom field on the property form. Code is only used on create (it is immutable).
public sealed record AttributeDefinitionInput(
  Guid AttributeGroupId,
  string Code,
  string Label,
  AttributeDataType DataType,
  bool IsRequired = false,
  string? OptionsCsv = null,
  string? DefaultValue = null,
  int DisplayOrder = 0);

public sealed record SaveAttributeDefinitionCommandResult(Guid Id);

/// Id null creates the field; otherwise updates it.
public sealed record SaveAttributeDefinitionCommand(Guid? Id, AttributeDefinitionInput Definition) : ICommand<Result<SaveAttributeDefinitionCommandResult>>;

public class SaveAttributeDefinitionCommandValidator : AbstractValidator<SaveAttributeDefinitionCommand>
{
  public SaveAttributeDefinitionCommandValidator()
  {
    RuleFor(x => x.Definition).NotNull();
    RuleFor(x => x.Definition.AttributeGroupId).NotEmpty();
    RuleFor(x => x.Definition.Code).NotEmpty().MaximumLength(MasterCode.MaxLength);
    RuleFor(x => x.Definition.Label).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Definition.DataType).IsInEnum();
    RuleFor(x => x.Definition.OptionsCsv).MaximumLength(2000);
    RuleFor(x => x.Definition.DefaultValue).MaximumLength(AttributeDefinition.MaxTextLength);
  }
}
