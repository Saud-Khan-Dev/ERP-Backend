public class ApproveTransferHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser, IDutyRules dutyRules, IAuditTrail audit)
  : ICommandHandler<ApproveTransferCommand, Result<ApproveTransferCommandResult>>
{
  public async Task<Result<ApproveTransferCommandResult>> Handle(ApproveTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);

    // the PROPERTY_TRANSFER rule (on unless a Super Administrator switched it off): the person who started - or recorded -
    // the transfer is not the one who approves it. Checked before anything is touched.
    var refusal = await dutyRules.SeparationRefusalAsync(DutyRules.PropertyTransfer, transfer.CreatedBy, currentUser.UserId,
      $"approve {transfer.TransferNo.Value}, which you {(transfer.IsRecordedPast ? "recorded" : "started")}", cancellationToken);
    if (refusal is not null)
      throw new DomainException(refusal);

    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, transfer.Parties.Select(p => p.OwnerId), cancellationToken);
    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);

    // a transfer recorded after the fact carries the approval read off the paper; the approver may correct it
    var change = transfer.Approve(
      TransferRegistration.Approver(currentUser),
      command.ApprovalDate ?? transfer.ApprovalDate ?? record.Today,
      command.ApprovalOrderRef ?? transfer.ApprovalOrderRef,
      command.NoDuesCertificateNo ?? transfer.NoDuesCertificateNo,
      TransferRegistration.ActorName(currentUser),
      record,
      tenure);

    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    audit.Record(PropertyAuditActions.Approved, transfer, Detail(transfer));
    await context.SaveChangesAsync(cancellationToken);

    return Result<ApproveTransferCommandResult>.Success(new ApproveTransferCommandResult(
      transfer.TransferStatus,
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }

  /// "Registered on 2026-10-21 · order DG/77 · no-dues CLR-00017".
  internal static string Detail(PropertyTransfer transfer) => string.Join(" · ", new[]
  {
    $"Ownership registered, approval dated {transfer.ApprovalDate:yyyy-MM-dd}",
    transfer.ApprovalOrderRef is { } order ? $"order {order}" : null,
    transfer.NoDuesCertificateNo is { } certificate ? $"no-dues certificate {certificate}" : null
  }.Where(x => x is not null));
}
