public class UpdatePropertyHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdatePropertyCommand, Result<UpdatePropertyCommandResult>>
{
  public async Task<Result<UpdatePropertyCommandResult>> Handle(UpdatePropertyCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.Id, cancellationToken);
    var input = command.Property;
    var set = await PropertyMasters.LoadAsync(masters, input, cancellationToken);
    var townChanged = set.Town.Id != property.TownId;
    // a property is moved only into a town the person works on
    if (townChanged)
      context.EnsureTownInScope(set.Town.Id, set.Town.Name.Value);

    // the classification decides whether the plot can be allotted: it must not cross an allotment being made
    context.Touch(property);

    property.UpdateDetails(
      Name.Of(input.PropertyName, Property.NameMaxLength),
      set.Town, set.Type, set.Classification,
      input.AddressLine, input.KhasraSurveyNo, input.Description, input.Remarks,
      await context.AllotmentInForceAsync(property.Id, except: null, cancellationToken));

    await context.SaveChangesAsync(cancellationToken);

    var warnings = townChanged
      ? new[] { $"{property.PropertyCode.Value} is now in {set.Town.Name.Value}. A tax file takes its number and circle from the town: ask Taxation to bring the property's file up to date." }
      : Array.Empty<string>();

    return Result<UpdatePropertyCommandResult>.Success(new UpdatePropertyCommandResult(true, warnings));
  }
}
