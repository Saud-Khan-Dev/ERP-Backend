public class CompleteTransferHandler(IApplicationDbContext context, MasterLookup masters)
  : ICommandHandler<CompleteTransferCommand, Result<CompleteTransferCommandResult>>
{
  public async Task<Result<CompleteTransferCommandResult>> Handle(CompleteTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    property.EnsureActive();

    var tenure = command.TenureTypeId is { } tenureId
      ? await masters.GetAsync<TenureType>(tenureId, cancellationToken)
      : await masters.GetByCodeAsync<TenureType>(SystemMasterCodes.TenureOwned, cancellationToken);

    var current = await context.CurrentOwnershipsAsync(property.Id, cancellationToken);
    var change = transfer.Complete(current, tenure);

    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CompleteTransferCommandResult>.Success(new CompleteTransferCommandResult(
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }
}
