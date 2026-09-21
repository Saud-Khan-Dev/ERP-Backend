using Microsoft.EntityFrameworkCore;

public class CreateLocationHandler(IApplicationDbContext context)
  : ICommandHandler<CreateLocationCommand, Result<CreateLocationCommandResult>>
{
  public async Task<Result<CreateLocationCommandResult>> Handle(CreateLocationCommand command, CancellationToken cancellationToken)
  {
    var input = command.Location;
    var code = LookupCode.Of(input.Code);

    if (await context.Locations.AnyAsync(l => l.Code == code, cancellationToken))
      return Result<CreateLocationCommandResult>.Failure($"A location with code {code.Value} already exists.");

    Location? parent = null;
    if (input.ParentLocationId.HasValue)
    {
      var parentId = LocationId.Of(input.ParentLocationId.Value);
      parent = await context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == parentId, cancellationToken)
        ?? throw new LocationNotFoundException($"Parent location {input.ParentLocationId} was not found.");
    }

    var location = Location.Create(
      LocationId.Of(Guid.NewGuid()), parent, code, Name.Of(input.Name, 150),
      input.LocationType, input.Address, input.Latitude, input.Longitude);

    if (!input.IsActive)
      location.Update(code, Name.Of(input.Name, 150), input.LocationType, input.Address, input.Latitude, input.Longitude, false);

    await context.Locations.AddAsync(location, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateLocationCommandResult>.Success(new CreateLocationCommandResult(location.Id.Value));
  }
}
