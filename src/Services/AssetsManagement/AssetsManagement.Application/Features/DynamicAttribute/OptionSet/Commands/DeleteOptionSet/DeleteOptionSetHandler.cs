using Microsoft.EntityFrameworkCore;

public class DeleteOptionSetHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteOptionSetCommand, Result<DeleteOptionSetCommandResult>>
{
  public async Task<Result<DeleteOptionSetCommandResult>> Handle(DeleteOptionSetCommand command, CancellationToken cancellationToken)
  {
    var id = OptionSetId.Of(command.Id);
    var optionSet = await context.OptionSets.Include(o => o.Values).FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
      ?? throw new OptionSetNotFoundException($"Option set {command.Id} was not found.");

    optionSet.EnsureDeletable();

    if (await context.AttributeDefinitions.AnyAsync(d => d.OptionSetId == id, cancellationToken))
      return Result<DeleteOptionSetCommandResult>.Failure("This option set is used by attribute definitions. Deactivate it instead.");

    context.OptionSets.Remove(optionSet);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteOptionSetCommandResult>.Success(new DeleteOptionSetCommandResult(true));
  }
}
