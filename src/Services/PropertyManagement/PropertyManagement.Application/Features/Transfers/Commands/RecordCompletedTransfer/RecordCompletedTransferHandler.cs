public class RecordCompletedTransferHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, ICurrentUser currentUser)
  : ICommandHandler<RecordCompletedTransferCommand, Result<RecordCompletedTransferCommandResult>>
{
  public async Task<Result<RecordCompletedTransferCommandResult>> Handle(RecordCompletedTransferCommand command, CancellationToken cancellationToken)
  {
    var input = command.Transfer;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var transferType = await masters.GetAsync<TransferType>(input.TransferTypeId, cancellationToken);

    var partyIds = await context.EnsureOwnersExistAsync(input.Parties.Select(p => p.OwnerId), cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, partyIds, cancellationToken);
    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);
    var actor = TransferRegistration.ActorName(currentUser);

    var (transfer, change) = PropertyTransfer.RecordCompleted(
      TransferId.New(), await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken), transferType,
      input.TransferDate, input.TransferReferenceNo, input.ConsiderationAmount, input.Relationship, input.Remarks,
      input.Parties.Select(p => new PropertyTransfer.PartyInput(OwnerId.Of(p.OwnerId), p.Role, p.SharePct)).ToList(),
      command.ApprovedBy ?? actor, command.ApprovalDate, command.ApprovalOrderRef, actor, record, tenure);

    await context.Transfers.AddAsync(transfer, cancellationToken);
    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordCompletedTransferCommandResult>.Success(new RecordCompletedTransferCommandResult(
      transfer.Id.Value, transfer.TransferNo.Value,
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }
}
