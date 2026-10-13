public class UpdateAllotmentHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<UpdateAllotmentCommand, Result<UpdateAllotmentCommandResult>>
{
  public async Task<Result<UpdateAllotmentCommandResult>> Handle(UpdateAllotmentCommand command, CancellationToken cancellationToken)
  {
    var allotment = await context.LoadAllotmentAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(allotment.PropertyId.Value, cancellationToken);
    var input = command.Allotment;

    allotment.Update(
      property,
      await masters.GetAsync<AllotmentType>(input.AllotmentTypeId, cancellationToken),
      input.AllotmentDate, input.EffectiveDate, input.ExpiryDate, input.AllotmentLetterRef, input.Conditions, input.Remarks,
      EntityLoaders.Today);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateAllotmentCommandResult>.Success(new UpdateAllotmentCommandResult(true));
  }
}
