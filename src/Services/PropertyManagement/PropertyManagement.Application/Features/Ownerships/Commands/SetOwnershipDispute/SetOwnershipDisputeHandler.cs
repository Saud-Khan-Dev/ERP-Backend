public class SetOwnershipDisputeHandler(IApplicationDbContext context)
  : ICommandHandler<SetOwnershipDisputeCommand, Result<SetOwnershipDisputeCommandResult>>
{
  public async Task<Result<SetOwnershipDisputeCommandResult>> Handle(SetOwnershipDisputeCommand command, CancellationToken cancellationToken)
  {
    var ownership = await context.LoadOwnershipAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(ownership.PropertyId.Value, cancellationToken);
    // a dispute decides whether the share can move: it must not cross a transfer being approved at the same moment
    context.Touch(property);

    // a disputed share stays in the 100% total, so resolving it never changes the total
    if (command.Disputed)
      ownership.MarkDisputed(property, command.Remarks);
    else
      ownership.ResolveDispute(property, command.Remarks);

    await context.SaveChangesAsync(cancellationToken);
    return Result<SetOwnershipDisputeCommandResult>.Success(new SetOwnershipDisputeCommandResult(ownership.OwnershipStatus));
  }
}
