using Microsoft.EntityFrameworkCore;

public class UpdateOptionSetValueHandler(IApplicationDbContext context, IAssetDataQueries queries)
  : ICommandHandler<UpdateOptionSetValueCommand, Result<UpdateOptionSetValueCommandResult>>
{
  public async Task<Result<UpdateOptionSetValueCommandResult>> Handle(UpdateOptionSetValueCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.OptionSetId);
    var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.OptionSetId} was not found.");

    var input = command.Value;
    var valueId = OptionSetValueId.Of(command.ValueId);
    var existing = optionSet.Values.FirstOrDefault(v => v.Id == valueId);
    var newCode = LookupCode.Of(input.Value);

    // the stored code is what assets hold in their details: changing it would cut those values off - renaming the
    // LABEL is always possible
    if (existing is not null && existing.Value != newCode)
    {
      var fieldCodes = await context.AttributeDefinitions.AsNoTracking().Where(d => d.OptionSetId == id).Select(d => d.Code).ToListAsync(cancellationToken);
      if (await queries.AssetsHoldingChoiceAsync(fieldCodes.Select(c => c.Value).ToList(), existing.Value.Value, cancellationToken) > 0)
        return Result<UpdateOptionSetValueCommandResult>.Failure($"Assets hold '{existing.Label}', so its code cannot change. Rename its label instead.");
    }

    optionSet.UpdateValue(
      valueId,
      input.ParentValueId.HasValue ? OptionSetValueId.Of(input.ParentValueId.Value) : null,
      newCode,
      input.Label,
      input.Color,
      input.Icon,
      input.DisplayOrder,
      input.IsActive);

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateOptionSetValueCommandResult>.Success(new UpdateOptionSetValueCommandResult(true));
  }
}
