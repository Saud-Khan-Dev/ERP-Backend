public class UpdateRegularizationHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<UpdateRegularizationCommand, Result<UpdateRegularizationCommandResult>>
{
  public async Task<Result<UpdateRegularizationCommandResult>> Handle(UpdateRegularizationCommand command, CancellationToken cancellationToken)
  {
    var regularization = await context.LoadRegularizationAsync(command.Id, cancellationToken);
    await context.EnsurePropertyActiveAsync(regularization.PropertyId, cancellationToken);
    var input = command.Case;
    RegularizationDecisions.EnsureMayDecide(currentUser, input.Status, regularization.RegularizationStatus);

    regularization.UpdateCase(
      new PropertyAreaRegularization.Decision(input.Status, input.RegularizationDate, input.OrderReferenceNo, input.ApprovedBy,
        input.ApprovedByEmployeeId, input.EffectiveFrom, input.EffectiveTo, input.Remarks),
      PropertyWriteGuard.Today());

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateRegularizationCommandResult>.Success(new UpdateRegularizationCommandResult(true));
  }
}
