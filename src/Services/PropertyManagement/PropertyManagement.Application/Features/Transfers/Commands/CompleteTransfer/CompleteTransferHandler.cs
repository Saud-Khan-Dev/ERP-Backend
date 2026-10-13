public class CompleteTransferHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser)
  : ICommandHandler<CompleteTransferCommand, Result<CompleteTransferCommandResult>>
{
  public async Task<Result<CompleteTransferCommandResult>> Handle(CompleteTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, transfer.Parties.Select(p => p.OwnerId), cancellationToken);
    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);

    var change = transfer.Complete(TransferRegistration.ActorName(currentUser), command.ApprovalOrderRef, record, tenure);

    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CompleteTransferCommandResult>.Success(new CompleteTransferCommandResult(
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }
}
