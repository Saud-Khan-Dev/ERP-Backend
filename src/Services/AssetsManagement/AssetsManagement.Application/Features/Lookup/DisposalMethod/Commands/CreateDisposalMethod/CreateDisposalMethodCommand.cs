using FluentValidation;

/// RequiresValue: proceeds must be entered (a sale). AllowsValue: proceeds may be entered (scrap sold for its value).
public sealed record DisposalMethodInput(string Code, string Name, bool RequiresValue = false, bool AllowsValue = true, bool IsActive = true);

public sealed record CreateDisposalMethodCommandResult(Guid Id);

public sealed record CreateDisposalMethodCommand(DisposalMethodInput Method) : ICommand<Result<CreateDisposalMethodCommandResult>>;

public class DisposalMethodInputValidator : AbstractValidator<DisposalMethodInput>
{
  public DisposalMethodInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().WithMessage("Give the method a code.").MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().WithMessage("Give the method a name.").MaximumLength(100);
    RuleFor(x => x).Must(x => !x.RequiresValue || x.AllowsValue).WithMessage("A method that requires the proceeds must also allow them.");
  }
}

public class CreateDisposalMethodCommandValidator : AbstractValidator<CreateDisposalMethodCommand>
{
  public CreateDisposalMethodCommandValidator()
  {
    RuleFor(x => x.Method).NotNull().SetValidator(new DisposalMethodInputValidator());
  }
}
