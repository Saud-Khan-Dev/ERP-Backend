public class SetOwnershipDisputeHandler(IApplicationDbContext context)
  : ICommandHandler<SetOwnershipDisputeCommand, Result<SetOwnershipDisputeCommandResult>>
{
  public async Task<Result<SetOwnershipDisputeCommandResult>> Handle(SetOwnershipDisputeCommand command, CancellationToken cancellationToken)
  {
    var ownership = await context.LoadOwnershipAsync(command.Id, cancellationToken);

    if (command.Disputed)
    {
      ownership.MarkDisputed(command.Remarks);
    }
    else
    {
      var current = await context.CurrentOwnershipsAsync(ownership.PropertyId, cancellationToken);
      OwnershipShares.EnsureRoomFor(current.Where(o => o.Id != ownership.Id), ownership.OwnershipSharePct);
      ownership.ResolveDispute(command.Remarks);
    }

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetOwnershipDisputeCommandResult>.Success(new SetOwnershipDisputeCommandResult(ownership.OwnershipStatus));
  }
}
