using Microsoft.EntityFrameworkCore;

public class DeleteCurrencyHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteCurrencyCommand, Result<DeleteCurrencyCommandResult>>
{
  public async Task<Result<DeleteCurrencyCommandResult>> Handle(DeleteCurrencyCommand command, CancellationToken cancellationToken)
  {
    var code = Currency.Of(command.Code);
    var currency = await context.Currencies.FirstOrDefaultAsync(c => c.Id == code, cancellationToken)
      ?? throw new CurrencyNotFoundException($"Currency {command.Code} was not found.");

    if (await context.AssetAcquisitions.AnyAsync(a => a.CurrencyCode == code, cancellationToken)
        || await context.AssetValuations.AnyAsync(v => v.CurrencyCode == code, cancellationToken)
        || await context.AssetDisposals.AnyAsync(d => d.CurrencyCode == code, cancellationToken))
      return Result<DeleteCurrencyCommandResult>.Failure("This currency is referenced by financial records. Deactivate it instead.");

    context.Currencies.Remove(currency);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteCurrencyCommandResult>.Success(new DeleteCurrencyCommandResult(true));
  }
}
