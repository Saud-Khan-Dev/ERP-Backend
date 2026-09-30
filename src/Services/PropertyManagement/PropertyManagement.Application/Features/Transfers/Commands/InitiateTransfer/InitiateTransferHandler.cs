using Microsoft.EntityFrameworkCore;

public class InitiateTransferHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes)
  : ICommandHandler<InitiateTransferCommand, Result<InitiateTransferCommandResult>>
{
  public async Task<Result<InitiateTransferCommandResult>> Handle(InitiateTransferCommand command, CancellationToken cancellationToken)
  {
    var input = command.Transfer;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var transferType = await masters.GetAsync<TransferType>(input.TransferTypeId, cancellationToken);

    // every party must be a registered, active owner record
    var partyIds = input.Parties.Select(p => OwnerId.Of(p.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => partyIds.Contains(o.Id)).ToListAsync(cancellationToken);

    var missing = partyIds.Except(owners.Select(o => o.Id)).FirstOrDefault();
    if (missing is not null)
      throw new OwnerNotFoundException($"Owner {missing.Value} was not found.");

    owners.ForEach(o => o.EnsureActive());

    var transfer = PropertyTransfer.Initiate(
      TransferId.New(),
      property,
      await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken),
      transferType,
      input.TransferDate,
      input.TransferReferenceNo,
      input.ConsiderationAmount,
      input.Relationship,
      input.Remarks,
      input.Parties.Select(p => new PropertyTransfer.PartyInput(OwnerId.Of(p.OwnerId), p.Role, p.SharePct)).ToList(),
      await context.CurrentOwnershipsAsync(property.Id, cancellationToken));

    await context.Transfers.AddAsync(transfer, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<InitiateTransferCommandResult>.Success(new InitiateTransferCommandResult(transfer.Id.Value, transfer.TransferNo.Value));
  }
}
