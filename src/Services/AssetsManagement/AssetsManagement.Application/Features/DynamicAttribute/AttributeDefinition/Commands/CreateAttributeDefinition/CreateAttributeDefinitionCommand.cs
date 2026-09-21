using FluentValidation;

public sealed record AttributeDefinitionInput(
  string Code,
  string Name,
  string? Description,
  AttributeDataType DataType,
  Guid? OptionSetId = null,
  string? ReferenceEntity = null,
  string? Unit = null,
  int? NumericPrecision = null,
  int? NumericScale = null,
  AttributeValidationRulesDto? Validation = null,
  bool IsMultiValue = false,
  bool IsPii = false,
  bool IsSystem = false,
  bool IsActive = true);

public sealed record CreateAttributeDefinitionCommandResult(Guid Id);

public sealed record CreateAttributeDefinitionCommand(AttributeDefinitionInput Definition) : ICommand<Result<CreateAttributeDefinitionCommandResult>>;

public class AttributeDefinitionInputValidator : AbstractValidator<AttributeDefinitionInput>
{
  public AttributeDefinitionInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleFor(x => x.DataType).IsInEnum();
    RuleFor(x => x.Unit).MaximumLength(50);
    RuleFor(x => x.ReferenceEntity).MaximumLength(100);
    RuleFor(x => x.OptionSetId)
      .NotEmpty()
      .When(x => x.DataType is AttributeDataType.Select or AttributeDataType.MultiSelect)
      .WithMessage("SELECT and MULTISELECT attributes require an option set.");
    RuleFor(x => x.ReferenceEntity)
      .NotEmpty()
      .When(x => x.DataType == AttributeDataType.Reference)
      .WithMessage("REFERENCE attributes must declare the referenced entity.");
  }
}

public class CreateAttributeDefinitionCommandValidator : AbstractValidator<CreateAttributeDefinitionCommand>
{
  public CreateAttributeDefinitionCommandValidator()
  {
    RuleFor(x => x.Definition).NotNull().SetValidator(new AttributeDefinitionInputValidator());
  }
}

public static class AttributeDefinitionInputExtensions
{
  public static AttributeValidationRules ToRules(this AttributeDefinitionInput input)
  {
    var v = input.Validation;
    return v is null
      ? new AttributeValidationRules()
      : new AttributeValidationRules(v.MinNumber, v.MaxNumber, v.MinLength, v.MaxLength, v.MinDate, v.MaxDate, v.RegexPattern, v.IsUniquePerCategory, v.ValidationMessage);
  }
}
