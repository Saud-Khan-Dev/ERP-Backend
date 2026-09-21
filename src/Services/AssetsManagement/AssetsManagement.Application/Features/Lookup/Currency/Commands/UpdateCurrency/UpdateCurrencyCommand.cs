using FluentValidation;

public sealed record UpdateCurrencyCommandResult(bool IsSuccess);

public sealed record UpdateCurrencyCommand(string Code, string Name, string? Symbol, short MinorUnits, bool IsActive)
  : ICommand<Result<UpdateCurrencyCommandResult>>;

public class UpdateCurrencyCommandValidator : AbstractValidator<UpdateCurrencyCommand>
{
  public UpdateCurrencyCommandValidator()
  {
    RuleFor(x => x.Code).NotEmpty().Length(3);
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Symbol).MaximumLength(10);
    RuleFor(x => x.MinorUnits).InclusiveBetween((short)0, (short)6);
  }
}
