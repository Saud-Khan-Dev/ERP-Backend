public class OpenRegularizationHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<OpenRegularizationCommand, Result<OpenRegularizationCommandResult>>
{
  public async Task<Result<OpenRegularizationCommandResult>> Handle(OpenRegularizationCommand command, CancellationToken cancellationToken)
  {
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var input = command.Regularization;
    RegularizationDecisions.EnsureMayDecide(currentUser, input.Status);
    var unit = await masters.GetAsync<MeasurementUnit>(input.MeasurementUnitId, cancellationToken);

    var regularization = PropertyAreaRegularization.Open(
      AreaRegularizationId.New(), property, unit, input.AdditionalArea, input.ApplicationDate,
      new PropertyAreaRegularization.Decision(input.Status, input.RegularizationDate, input.OrderReferenceNo, input.ApprovedBy,
        input.ApprovedByEmployeeId, input.EffectiveFrom, null, input.Remarks),
      PropertyWriteGuard.Today());

    await context.AreaRegularizations.AddAsync(regularization, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<OpenRegularizationCommandResult>.Success(new OpenRegularizationCommandResult(regularization.Id.Value));
  }
}
