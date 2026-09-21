using FluentValidation;

public sealed record DisposalMethodInput(string Code, string Name, bool RequiresValue = false, bool IsActive = true);

public sealed record CreateDisposalMethodCommandResult(Guid Id);

public sealed record CreateDisposalMethodCommand(DisposalMethodInput Method) : ICommand<Result<CreateDisposalMethodCommandResult>>;

public class DisposalMethodInputValidator : AbstractValidator<DisposalMethodInput>
{
  public DisposalMethodInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
  }
}

public class CreateDisposalMethodCommandValidator : AbstractValidator<CreateDisposalMethodCommand>
{
  public CreateDisposalMethodCommandValidator()
  {
    RuleFor(x => x.Method).NotNull().SetValidator(new DisposalMethodInputValidator());
  }
}
