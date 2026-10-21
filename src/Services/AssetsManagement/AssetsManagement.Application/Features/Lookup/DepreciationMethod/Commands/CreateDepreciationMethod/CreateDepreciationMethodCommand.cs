using FluentValidation;

/// Kind: how the method is worked out (straight line, declining balance, units of production).
public sealed record DepreciationMethodInput(string Code, string Name, string? Description, DepreciationMethodKind Kind = DepreciationMethodKind.StraightLine, bool IsActive = true);

public sealed record CreateDepreciationMethodCommandResult(Guid Id);

public sealed record CreateDepreciationMethodCommand(DepreciationMethodInput Method) : ICommand<Result<CreateDepreciationMethodCommandResult>>;

public class DepreciationMethodInputValidator : AbstractValidator<DepreciationMethodInput>
{
  public DepreciationMethodInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().WithMessage("Give the method a code.").MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().WithMessage("Give the method a name.").MaximumLength(100);
    RuleFor(x => x.Kind).IsInEnum().WithMessage("Choose how the method is worked out.");
  }
}

public class CreateDepreciationMethodCommandValidator : AbstractValidator<CreateDepreciationMethodCommand>
{
  public CreateDepreciationMethodCommandValidator()
  {
    RuleFor(x => x.Method).NotNull().SetValidator(new DepreciationMethodInputValidator());
  }
}
