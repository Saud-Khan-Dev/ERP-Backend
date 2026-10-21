using Microsoft.EntityFrameworkCore;

public class RemoveOptionSetValueHandler(IApplicationDbContext context, IAssetDataQueries queries)
  : ICommandHandler<RemoveOptionSetValueCommand, Result<RemoveOptionSetValueCommandResult>>
{
  public async Task<Result<RemoveOptionSetValueCommandResult>> Handle(RemoveOptionSetValueCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.OptionSetId);
    var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.OptionSetId} was not found.");

    var valueId = OptionSetValueId.Of(command.ValueId);
    var value = optionSet.Values.FirstOrDefault(v => v.Id == valueId)
      ?? throw new DomainException("That choice is not on this list.");

    // held in any asset's details (not only the filter rows) of any field that uses this list
    var fieldCodes = await context.AttributeDefinitions.AsNoTracking().Where(d => d.OptionSetId == id).Select(d => d.Code).ToListAsync(cancellationToken);
    var holders = await queries.AssetsHoldingChoiceAsync(fieldCodes.Select(c => c.Value).ToList(), value.Value.Value, cancellationToken);
    if (holders > 0)
      return Result<RemoveOptionSetValueCommandResult>.Failure($"{holders} {(holders == 1 ? "asset holds" : "assets hold")} '{value.Label}', so it cannot be removed. Switch it off instead - those assets keep it.");

    optionSet.RemoveValue(valueId);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RemoveOptionSetValueCommandResult>.Success(new RemoveOptionSetValueCommandResult(true));
  }
}
