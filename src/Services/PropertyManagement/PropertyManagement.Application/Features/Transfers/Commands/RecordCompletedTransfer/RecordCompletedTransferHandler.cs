using Microsoft.EntityFrameworkCore;

public class RecordCompletedTransferHandler(IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, ICurrentUser currentUser)
  : ICommandHandler<RecordCompletedTransferCommand, Result<RecordCompletedTransferCommandResult>>
{
  public async Task<Result<RecordCompletedTransferCommandResult>> Handle(RecordCompletedTransferCommand command, CancellationToken cancellationToken)
  {
    var input = command.Transfer;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var transferType = await masters.GetAsync<TransferType>(input.TransferTypeId, cancellationToken);

    var partyIds = input.Parties.Select(p => OwnerId.Of(p.OwnerId)).Distinct().ToList();
    var owners = await context.Owners.AsNoTracking().Where(o => partyIds.Contains(o.Id)).ToListAsync(cancellationToken);

    var missing = partyIds.Except(owners.Select(o => o.Id)).FirstOrDefault();
    if (missing is not null)
      throw new OwnerNotFoundException($"Owner {missing.Value} was not found.");

    owners.ForEach(o => o.EnsureActive());

    var current = await context.CurrentOwnershipsAsync(property.Id, cancellationToken);

    var transfer = PropertyTransfer.Initiate(
      TransferId.New(), property, await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken), transferType,
      input.TransferDate, input.TransferReferenceNo, input.ConsiderationAmount, input.Relationship, input.Remarks,
      input.Parties.Select(p => new PropertyTransfer.PartyInput(OwnerId.Of(p.OwnerId), p.Role, p.SharePct)).ToList(),
      current);

    transfer.Approve(
      command.ApprovedBy ?? currentUser.Username ?? currentUser.AuditName,
      command.ApprovalDate ?? input.TransferDate);

    var tenure = command.TenureTypeId is { } tenureId
      ? await masters.GetAsync<TenureType>(tenureId, cancellationToken)
      : await masters.GetByCodeAsync<TenureType>(SystemMasterCodes.TenureOwned, cancellationToken);

    var change = transfer.Complete(current, tenure);

    await context.Transfers.AddAsync(transfer, cancellationToken);
    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordCompletedTransferCommandResult>.Success(new RecordCompletedTransferCommandResult(
      transfer.Id.Value, transfer.TransferNo.Value,
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }
}
