using Microsoft.EntityFrameworkCore;

public class RemoveOptionSetValueHandler(IApplicationDbContext context)
  : ICommandHandler<RemoveOptionSetValueCommand, Result<RemoveOptionSetValueCommandResult>>
{
  public async Task<Result<RemoveOptionSetValueCommandResult>> Handle(RemoveOptionSetValueCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.OptionSetId);
    var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.OptionSetId} was not found.");

    var valueId = OptionSetValueId.Of(command.ValueId);

    if (await context.AssetAttributeValues.AnyAsync(v => v.OptionValueId == valueId, cancellationToken))
      return Result<RemoveOptionSetValueCommandResult>.Failure("This option value is used by assets and cannot be removed. Deactivate it instead.");

    optionSet.RemoveValue(valueId);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RemoveOptionSetValueCommandResult>.Success(new RemoveOptionSetValueCommandResult(true));
  }
}
