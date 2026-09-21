using FluentValidation;

public sealed record OptionSetValueInput(
  string Value,
  string Label,
  Guid? ParentValueId = null,
  string? Color = null,
  string? Icon = null,
  int? DisplayOrder = null,
  bool IsActive = true);

public sealed record OptionSetInput(
  string Code,
  string Name,
  string? Description,
  bool IsSystem = false,
  bool IsActive = true,
  IReadOnlyList<OptionSetValueInput>? Values = null);

public sealed record CreateOptionSetCommandResult(Guid Id);

public sealed record CreateOptionSetCommand(OptionSetInput OptionSet) : ICommand<Result<CreateOptionSetCommandResult>>;

public class OptionSetValueInputValidator : AbstractValidator<OptionSetValueInput>
{
  public OptionSetValueInputValidator()
  {
    RuleFor(x => x.Value).NotEmpty().MaximumLength(255);
    RuleFor(x => x.Label).NotEmpty().MaximumLength(255);
    RuleFor(x => x.Color).MaximumLength(20);
    RuleFor(x => x.Icon).MaximumLength(100);
  }
}

public class OptionSetInputValidator : AbstractValidator<OptionSetInput>
{
  public OptionSetInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    RuleForEach(x => x.Values).SetValidator(new OptionSetValueInputValidator());
  }
}

public class CreateOptionSetCommandValidator : AbstractValidator<CreateOptionSetCommand>
{
  public CreateOptionSetCommandValidator()
  {
    RuleFor(x => x.OptionSet).NotNull().SetValidator(new OptionSetInputValidator());
  }
}
