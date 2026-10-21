using Microsoft.EntityFrameworkCore;

public class DeleteLocationHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteLocationCommand, Result<DeleteLocationCommandResult>>
{
  public async Task<Result<DeleteLocationCommandResult>> Handle(DeleteLocationCommand command, CancellationToken cancellationToken)
  {
    var id = LocationId.Of(command.Id);
    var location = await context.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
      ?? throw new LocationNotFoundException($"Location {command.Id} was not found.");

    if (await context.Locations.AnyAsync(l => l.ParentLocationId == id, cancellationToken))
      return Result<DeleteLocationCommandResult>.Failure($"'{location.Name.Value}' has places inside it. Delete or move them first.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CurrentLocationId == id, cancellationToken)
        || await context.AssetAssignments.AnyAsync(a => a.FromLocationId == id || a.ToLocationId == id, cancellationToken))
      return Result<DeleteLocationCommandResult>.Failure($"Assets are at '{location.Name.Value}', or were moved through it, so it cannot be deleted. Switch it off instead - it then stays readable on old records.");

    context.Locations.Remove(location);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteLocationCommandResult>.Success(new DeleteLocationCommandResult(true));
  }
}
