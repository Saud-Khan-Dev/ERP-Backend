public class RecordCompletedTransferHandler(
    IApplicationDbContext context, MasterLookup masters, CodeGenerator codes, ICurrentUser currentUser, IDutyRules dutyRules, IAuditTrail audit)
  : ICommandHandler<RecordCompletedTransferCommand, Result<RecordCompletedTransferCommandResult>>
{
  public async Task<Result<RecordCompletedTransferCommandResult>> Handle(RecordCompletedTransferCommand command, CancellationToken cancellationToken)
  {
    var input = command.Transfer;
    var property = await context.LoadPropertyAsync(command.PropertyId, cancellationToken);
    var transferType = await masters.GetAsync<TransferType>(input.TransferTypeId, cancellationToken);

    var partyIds = await context.EnsureOwnersExistAsync(input.Parties.Select(p => p.OwnerId), cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, partyIds, cancellationToken);
    var parties = input.Parties.Select(p => new PropertyTransfer.PartyInput(OwnerId.Of(p.OwnerId), p.Role, p.SharePct)).ToList();

    // while two people must pass every transfer (PROPERTY_TRANSFER), a past transfer is entered for a second officer to
    // approve - one person cannot move ownership alone by calling it "already happened"
    if (await dutyRules.IsOnAsync(DutyRules.PropertyTransfer, cancellationToken))
    {
      var waiting = PropertyTransfer.RecordForApproval(
        TransferId.New(), await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken), transferType,
        input.TransferDate, input.TransferReferenceNo, input.ConsiderationAmount, input.Relationship, input.Remarks, parties,
        command.ApprovalDate, command.ApprovalOrderRef, command.NoDuesCertificateNo, record);

      await context.Transfers.AddAsync(waiting, cancellationToken);
      audit.Record(PropertyAuditActions.RecordedForApproval, waiting,
        "Entered after the fact; waits for another officer's approval (Transfers pass through two people)");
      await context.SaveChangesAsync(cancellationToken);

      return Result<RecordCompletedTransferCommandResult>.Success(new RecordCompletedTransferCommandResult(
        waiting.Id.Value, waiting.TransferNo.Value, [], [], waiting.TransferStatus, AwaitingApproval: true));
    }

    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);
    var actor = TransferRegistration.ActorName(currentUser);

    var (transfer, change) = PropertyTransfer.RecordCompleted(
      TransferId.New(), await codes.NextAsync(CodeSequenceKeys.Transfer, cancellationToken), transferType,
      input.TransferDate, input.TransferReferenceNo, input.ConsiderationAmount, input.Relationship, input.Remarks, parties,
      TransferRegistration.Approver(currentUser), command.ApprovalDate, command.ApprovalOrderRef, command.NoDuesCertificateNo,
      actor, record, tenure);

    await context.Transfers.AddAsync(transfer, cancellationToken);
    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    audit.Record(PropertyAuditActions.Approved, transfer, "Recorded as completed in one step (the two-person rule is off) · " + ApproveTransferHandler.Detail(transfer));
    await context.SaveChangesAsync(cancellationToken);

    return Result<RecordCompletedTransferCommandResult>.Success(new RecordCompletedTransferCommandResult(
      transfer.Id.Value, transfer.TransferNo.Value,
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList(),
      transfer.TransferStatus));
  }
}
