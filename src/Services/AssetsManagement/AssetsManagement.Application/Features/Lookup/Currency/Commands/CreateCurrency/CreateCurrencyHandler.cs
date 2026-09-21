using Microsoft.EntityFrameworkCore;

public class CreateCurrencyHandler(IApplicationDbContext context)
  : ICommandHandler<CreateCurrencyCommand, Result<CreateCurrencyCommandResult>>
{
  public async Task<Result<CreateCurrencyCommandResult>> Handle(CreateCurrencyCommand command, CancellationToken cancellationToken)
  {
    var input = command.Currency;
    var code = Currency.Of(input.Code);

    if (await context.Currencies.AnyAsync(c => c.Id == code, cancellationToken))
      return Result<CreateCurrencyCommandResult>.Failure($"Currency {code.Value} already exists.");

    var currency = CurrencyLookup.Create(code, Name.Of(input.Name, 100), input.Symbol, input.MinorUnits);

    if (!input.IsActive)
      currency.Update(Name.Of(input.Name, 100), input.Symbol, input.MinorUnits, false);

    await context.Currencies.AddAsync(currency, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateCurrencyCommandResult>.Success(new CreateCurrencyCommandResult(currency.Id.Value));
  }
}
