public class OpenRegularizationHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<OpenRegularizationCommand, Result<OpenRegularizationCommandResult>>
{
  public async Task<Result<OpenRegularizationCommandResult>> Handle(OpenRegularizationCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Regularization;
    var unit = await masters.GetAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken);

    var regularization = PropertyAreaRegularization.Open(
      AreaRegularizationId.New(), property, unit, input.AdditionalArea, input.Status, input.ApplicationDate,
      input.RegularizationDate, input.OrderReferenceNo, input.ApprovedBy, input.EffectiveFrom, input.Remarks);

    await context.AreaRegularizations.AddAsync(regularization, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<OpenRegularizationCommandResult>.Success(new OpenRegularizationCommandResult(regularization.Id.Value));
  }
}
