using FluentValidation;

public sealed record CurrencyInput(string Code, string Name, string? Symbol, short MinorUnits = 2, bool IsActive = true);

public sealed record CreateCurrencyCommandResult(string Code);

public sealed record CreateCurrencyCommand(CurrencyInput Currency) : ICommand<Result<CreateCurrencyCommandResult>>;

public class CurrencyInputValidator : AbstractValidator<CurrencyInput>
{
  public CurrencyInputValidator()
  {
    RuleFor(x => x.Code).NotEmpty().Length(3).WithMessage("Currency code must be a 3-letter ISO 4217 code.");
    RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    RuleFor(x => x.Symbol).MaximumLength(10);
    RuleFor(x => x.MinorUnits).InclusiveBetween((short)0, (short)6);
  }
}

public class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
  public CreateCurrencyCommandValidator()
  {
    RuleFor(x => x.Currency).NotNull().SetValidator(new CurrencyInputValidator());
  }
}
