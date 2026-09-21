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
      return Result<DeleteLocationCommandResult>.Failure("This location has child locations. Delete or move them first.");

    if (await context.Assets.IgnoreQueryFilters().AnyAsync(a => a.CurrentLocationId == id, cancellationToken)
        || await context.AssetAssignments.AnyAsync(a => a.FromLocationId == id || a.ToLocationId == id, cancellationToken))
      return Result<DeleteLocationCommandResult>.Failure("This location is referenced by assets or assignment history. Deactivate it instead.");

    context.Locations.Remove(location);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteLocationCommandResult>.Success(new DeleteLocationCommandResult(true));
  }
}
