using FluentValidation;

public sealed record DeleteDisposalMethodCommandResult(bool IsSuccess);

public sealed record DeleteDisposalMethodCommand(Guid Id) : ICommand<Result<DeleteDisposalMethodCommandResult>>;

public class DeleteDisposalMethodCommandValidator : AbstractValidator<DeleteDisposalMethodCommand>
{
  public DeleteDisposalMethodCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
  }
}
