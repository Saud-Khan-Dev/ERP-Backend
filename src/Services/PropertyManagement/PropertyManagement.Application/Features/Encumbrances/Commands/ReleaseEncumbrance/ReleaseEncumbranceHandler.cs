public class ReleaseEncumbranceHandler(IApplicationDbContext context, IAuditTrail audit)
  : ICommandHandler<ReleaseEncumbranceCommand, Result<ReleaseEncumbranceCommandResult>>
{
  public async Task<Result<ReleaseEncumbranceCommandResult>> Handle(ReleaseEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var encumbrance = await context.LoadEncumbranceAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(encumbrance.PropertyId.Value, cancellationToken);
    encumbrance.Release(property, command.ReleaseDate, command.ReleaseReferenceNo, EntityLoaders.Today);
    audit.Record(PropertyAuditActions.Released, encumbrance, command.ReleaseReferenceNo);
    context.Touch(property);

    await context.SaveChangesAsync(cancellationToken);
    return Result<ReleaseEncumbranceCommandResult>.Success(new ReleaseEncumbranceCommandResult(encumbrance.Status));
  }
}
