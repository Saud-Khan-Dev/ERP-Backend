using Microsoft.EntityFrameworkCore;

public class AddOptionSetValueHandler(IApplicationDbContext context)
  : ICommandHandler<AddOptionSetValueCommand, Result<AddOptionSetValueCommandResult>>
{
  public async Task<Result<AddOptionSetValueCommandResult>> Handle(AddOptionSetValueCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.OptionSetId);
    var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.OptionSetId} was not found.");

    var input = command.Value;

    var value = optionSet.AddValue(
      OptionSetValueId.Of(Guid.NewGuid()),
      input.ParentValueId.HasValue ? OptionSetValueId.Of(input.ParentValueId.Value) : null,
      LookupCode.Of(input.Value),
      input.Label,
      input.Color,
      input.Icon,
      input.DisplayOrder);

    if (!input.IsActive)
      optionSet.UpdateValue(value.Id, value.ParentValueId, value.Value, value.Label, value.Color, value.Icon, value.DisplayOrder, false);

    await context.SaveChangesAsync(cancellationToken);

    return Result<AddOptionSetValueCommandResult>.Success(new AddOptionSetValueCommandResult(value.Id.Value));
  }
}
