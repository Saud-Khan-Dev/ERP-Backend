using Microsoft.EntityFrameworkCore;

public class UpdateCurrencyHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateCurrencyCommand, Result<UpdateCurrencyCommandResult>>
{
  public async Task<Result<UpdateCurrencyCommandResult>> Handle(UpdateCurrencyCommand command, CancellationToken cancellationToken)
  {
    var code = Currency.Of(command.Code);
    var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == code, cancellationToken)
      ?? throw new CurrencyNotFoundException($"Currency {command.Code} was not found.");

    currency.Update(Name.Of(command.Name, 100), command.Symbol, command.MinorUnits, command.IsActive);
    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateCurrencyCommandResult>.Success(new UpdateCurrencyCommandResult(true));
  }
}
