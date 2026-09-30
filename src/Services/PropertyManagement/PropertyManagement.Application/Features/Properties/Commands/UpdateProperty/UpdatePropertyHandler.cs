public class UpdatePropertyHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdatePropertyCommand, Result<UpdatePropertyCommandResult>>
{
  public async Task<Result<UpdatePropertyCommandResult>> Handle(UpdatePropertyCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.Id, cancellationToken);
    var input = command.Property;
    var set = await PropertyMasters.LoadAsync(masters, input, cancellationToken);

    property.UpdateDetails(
      Name.Of(input.PropertyName, Property.NameMaxLength),
      set.Town, set.Type, set.Classification,
      input.AddressLine, input.KhasraSurveyNo, input.Description, input.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdatePropertyCommandResult>.Success(new UpdatePropertyCommandResult(true));
  }
}
