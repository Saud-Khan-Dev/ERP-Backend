using Microsoft.EntityFrameworkCore;

public class UpdateOptionSetValueHandler(IApplicationDbContext context)
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

    // the stored code is what assets hold in extra_attributes: renaming it would orphan their values
    if (existing is not null && existing.Value != newCode
        && await context.AssetAttributeValues.AnyAsync(v => v.OptionValueId == valueId, cancellationToken))
      return Result<UpdateOptionSetValueCommandResult>.Failure("This option value is already used by assets; its stored code cannot be changed. Deactivate it and add a new one instead.");

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
