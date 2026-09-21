using Microsoft.EntityFrameworkCore;

public class UpdateLocationHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateLocationCommand, Result<UpdateLocationCommandResult>>
{
  public async Task<Result<UpdateLocationCommandResult>> Handle(UpdateLocationCommand command, CancellationToken cancellationToken)
  {
    var id = LocationId.Of(command.Id);
    var input = command.Location;

    // paths of every descendant may need rebuilding, so the tree is loaded once
    var tree = await context.Locations.ToListAsync(cancellationToken);
    var location = tree.FirstOrDefault(l => l.Id == id)
      ?? throw new LocationNotFoundException($"Location {command.Id} was not found.");

    var code = LookupCode.Of(input.Code);
    if (tree.Any(l => l.Id != id && l.Code == code))
      return Result<UpdateLocationCommandResult>.Failure($"A location with code {code.Value} already exists.");

    Location? parent = null;
    if (input.ParentLocationId.HasValue)
    {
      var parentId = LocationId.Of(input.ParentLocationId.Value);
      parent = tree.FirstOrDefault(l => l.Id == parentId)
        ?? throw new LocationNotFoundException($"Parent location {input.ParentLocationId} was not found.");
    }

    location.Update(code, Name.Of(input.Name, 150), input.LocationType, input.Address, input.Latitude, input.Longitude, input.IsActive);
    location.Rebase(parent);

    var queue = new Queue<Location>();
    queue.Enqueue(location);
    while (queue.Count > 0)
    {
      var current = queue.Dequeue();
      foreach (var child in tree.Where(l => l.ParentLocationId == current.Id))
      {
        child.Rebase(current);
        queue.Enqueue(child);
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<UpdateLocationCommandResult>.Success(new UpdateLocationCommandResult(true));
  }
}
