public class DeactivateRegularizationHandler(IApplicationDbContext context)
  : ICommandHandler<DeactivateRegularizationCommand, Result<DeactivateRegularizationCommandResult>>
{
  public async Task<Result<DeactivateRegularizationCommandResult>> Handle(DeactivateRegularizationCommand command, CancellationToken cancellationToken)
  {
    var regularization = await context.LoadRegularizationAsync(command.Id, cancellationToken);
    regularization.Deactivate();

    await context.SaveChangesAsync(cancellationToken);
    return Result<DeactivateRegularizationCommandResult>.Success(new DeactivateRegularizationCommandResult(true));
  }
}
