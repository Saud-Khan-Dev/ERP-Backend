public class InitiateTransferHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<InitiateTransferCommand, Result<InitiateTransferCommandResult>>
{
  public async Task<Result<InitiateTransferCommandResult>> Handle(InitiateTransferCommand command, CancellationToken cancellationToken)
  {
    var input = command.Transfer;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var transferType = await masters.GetAsync<TransferType>(input.TransferTypeId, cancellationToken);

    // every party must be a registered owner record (the domain checks they are active)
    var partyIds = await context.EnsureOwnersExistAsync(input.Parties.Select(p => p.OwnerId), cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, partyIds, cancellationToken);

    var transfer = PropertyTransfer.Initiate(
      TransferId.New(),
      await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken),
      transferType,
      input.TransferDate,
      input.TransferReferenceNo,
      input.ConsiderationAmount,
      input.Relationship,
      input.Remarks,
      input.Parties.Select(p => new PropertyTransfer.PartyInput(OwnerId.Of(p.OwnerId), p.Role, p.SharePct)).ToList(),
      record);

    await context.Transfers.AddAsync(transfer, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<InitiateTransferCommandResult>.Success(new InitiateTransferCommandResult(transfer.Id.Value, transfer.TransferNo.Value));
  }
}
