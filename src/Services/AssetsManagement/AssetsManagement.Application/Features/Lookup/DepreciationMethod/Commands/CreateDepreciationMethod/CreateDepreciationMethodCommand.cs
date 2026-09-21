using FluentValidation;

public sealed record DepreciationMethodInput(string Code, string Name, string? Description, bool IsActive = true);

public sealed record CreateDepreciationMethodCommandResult(Guid Id);

public sealed record CreateDepreciationMethodCommand(DepreciationMethodInput Method) : ICommand<Result<CreateDepreciationMethodCommandResult>>;

public class DepreciationMethodInputValidator : AbstractValidator<DepreciationMethodInput>
{
  public DepreciationMethodInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
  }
}

public class CreateDepreciationMethodCommandValidator : AbstractValidator<CreateDepreciationMethodCommand>
{
  public CreateDepreciationMethodCommandValidator()
  {
    RuleFor(x => x.Method).NotNull().SetValidator(new DepreciationMethodInputValidator());
  }
}
