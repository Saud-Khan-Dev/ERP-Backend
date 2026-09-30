public class EndOwnershipHandler(IApplicationDbContext context)
  : ICommandHandler<EndOwnershipCommand, Result<EndOwnershipCommandResult>>
{
  public async Task<Result<EndOwnershipCommandResult>> Handle(EndOwnershipCommand command, CancellationToken cancellationToken)
  {
    var ownership = await context.LoadOwnershipAsync(command.Id, cancellationToken);
    ownership.End(command.EffectiveTo, command.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<EndOwnershipCommandResult>.Success(new EndOwnershipCommandResult(true));
  }
}
