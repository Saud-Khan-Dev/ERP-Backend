using FluentValidation;

public sealed record UpdateDisposalMethodCommandResult(bool IsSuccess);

public sealed record UpdateDisposalMethodCommand(Guid Id, DisposalMethodInput Method) : ICommand<Result<UpdateDisposalMethodCommandResult>>;

public class UpdateDisposalMethodCommandValidator : AbstractValidator<UpdateDisposalMethodCommand>
{
  public UpdateDisposalMethodCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Method).NotNull().SetValidator(new DisposalMethodInputValidator());
  }
}
