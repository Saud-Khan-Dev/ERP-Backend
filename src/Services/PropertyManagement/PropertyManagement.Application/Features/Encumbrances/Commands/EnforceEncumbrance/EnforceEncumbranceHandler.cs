public class EnforceEncumbranceHandler(IApplicationDbContext context)
  : ICommandHandler<EnforceEncumbranceCommand, Result<EnforceEncumbranceCommandResult>>
{
  public async Task<Result<EnforceEncumbranceCommandResult>> Handle(EnforceEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var encumbrance = await context.LoadEncumbranceAsync(command.Id, cancellationToken);
    encumbrance.Enforce(command.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<EnforceEncumbranceCommandResult>.Success(new EnforceEncumbranceCommandResult(encumbrance.Status));
  }
}
