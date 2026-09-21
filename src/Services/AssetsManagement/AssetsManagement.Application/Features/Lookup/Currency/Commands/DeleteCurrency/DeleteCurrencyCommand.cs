using FluentValidation;

public sealed record DeleteCurrencyCommandResult(bool IsSuccess);

public sealed record DeleteCurrencyCommand(string Code) : ICommand<Result<DeleteCurrencyCommandResult>>;

public class DeleteCurrencyCommandValidator : AbstractValidator<DeleteCurrencyCommand>
{
  public DeleteCurrencyCommandValidator()
  {
    RuleFor(x => x.Code).NotEmpty().Length(3);
  }
}
