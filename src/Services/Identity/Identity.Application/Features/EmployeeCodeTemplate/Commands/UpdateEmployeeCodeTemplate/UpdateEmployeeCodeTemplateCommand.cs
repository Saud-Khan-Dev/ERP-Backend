using FluentValidation;

/// Prefix "EMP", separator "-", minimum digits 3, next number 101  →  the next code is EMP-101.
public sealed record EmployeeCodeTemplateInput(string Prefix, string? Separator, int MinimumDigits, long NextNumber);

public sealed record UpdateEmployeeCodeTemplateCommandResult(EmployeeCodeTemplateDto Template);

public sealed record UpdateEmployeeCodeTemplateCommand(EmployeeCodeTemplateInput Template)
  : ICommand<Result<UpdateEmployeeCodeTemplateCommandResult>>;

public class EmployeeCodeTemplateInputValidator : AbstractValidator<EmployeeCodeTemplateInput>
{
  public EmployeeCodeTemplateInputValidator()
  {
    RuleFor(x => x.Prefix).NotEmpty().MaximumLength(EmployeeCodeTemplate.MaxPrefixLength);
    RuleFor(x => x.Separator).MaximumLength(1);
    RuleFor(x => x.MinimumDigits).InclusiveBetween(1, EmployeeCodeTemplate.MaxMinimumDigits);
    RuleFor(x => x.NextNumber).InclusiveBetween(1, EmployeeCodeTemplate.MaxNumber);
  }
}

public class UpdateEmployeeCodeTemplateCommandValidator : AbstractValidator<UpdateEmployeeCodeTemplateCommand>
{
  public UpdateEmployeeCodeTemplateCommandValidator()
  {
    RuleFor(x => x.Template).NotNull().SetValidator(new EmployeeCodeTemplateInputValidator());
  }
}
