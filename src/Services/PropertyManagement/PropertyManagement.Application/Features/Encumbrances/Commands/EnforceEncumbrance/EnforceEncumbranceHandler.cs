public class EnforceEncumbranceHandler(IApplicationDbContext context)
  : ICommandHandler<EnforceEncumbranceCommand, Result<EnforceEncumbranceCommandResult>>
{
  public async Task<Result<EnforceEncumbranceCommandResult>> Handle(EnforceEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var encumbrance = await context.LoadEncumbranceAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(encumbrance.PropertyId.Value, cancellationToken);
    encumbrance.Enforce(property, command.Remarks);
    context.Touch(property);

    await context.SaveChangesAsync(cancellationToken);
    return Result<EnforceEncumbranceCommandResult>.Success(new EnforceEncumbranceCommandResult(encumbrance.Status));
  }
}
