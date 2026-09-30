public class ReleaseEncumbranceHandler(IApplicationDbContext context)
  : ICommandHandler<ReleaseEncumbranceCommand, Result<ReleaseEncumbranceCommandResult>>
{
  public async Task<Result<ReleaseEncumbranceCommandResult>> Handle(ReleaseEncumbranceCommand command, CancellationToken cancellationToken)
  {
    var encumbrance = await context.LoadEncumbranceAsync(command.Id, cancellationToken);
    encumbrance.Release(command.ReleaseDate, command.ReleaseReferenceNo);

    await context.SaveChangesAsync(cancellationToken);
    return Result<ReleaseEncumbranceCommandResult>.Success(new ReleaseEncumbranceCommandResult(encumbrance.Status));
  }
}
