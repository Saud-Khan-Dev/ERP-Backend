public class UpdateRegularizationHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateRegularizationCommand, Result<UpdateRegularizationCommandResult>>
{
  public async Task<Result<UpdateRegularizationCommandResult>> Handle(UpdateRegularizationCommand command, CancellationToken cancellationToken)
  {
    var regularization = await context.LoadRegularizationAsync(command.Id, cancellationToken);
    var input = command.Case;

    regularization.UpdateCase(input.Status, input.RegularizationDate, input.OrderReferenceNo, input.ApprovedBy,
      input.EffectiveFrom, input.EffectiveTo, input.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<UpdateRegularizationCommandResult>.Success(new UpdateRegularizationCommandResult(true));
  }
}
