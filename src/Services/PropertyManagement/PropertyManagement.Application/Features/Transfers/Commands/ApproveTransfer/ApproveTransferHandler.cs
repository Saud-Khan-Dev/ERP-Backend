using Microsoft.Extensions.Options;

public class ApproveTransferHandler(IApplicationDbContext context, MasterLookup masters, ICurrentUser currentUser, IOptions<TransferOptions> options)
  : ICommandHandler<ApproveTransferCommand, Result<ApproveTransferCommandResult>>
{
  public async Task<Result<ApproveTransferCommandResult>> Handle(ApproveTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    var record = await context.LoadOwnershipRecordAsync(property, transfer.Parties.Select(p => p.OwnerId), cancellationToken);
    var tenure = await TransferRegistration.NewcomerTenureAsync(masters, command.TenureTypeId, cancellationToken);

    var change = transfer.Approve(
      TransferRegistration.Approver(currentUser),
      command.ApprovalDate ?? record.Today,
      command.ApprovalOrderRef,
      command.NoDuesCertificateNo,
      TransferRegistration.ActorName(currentUser),
      record,
      tenure,
      options.Value.RequireSeparateApprover);

    await context.Ownerships.AddRangeAsync(change.Opened, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ApproveTransferCommandResult>.Success(new ApproveTransferCommandResult(
      transfer.TransferStatus,
      change.Closed.Select(o => o.Id.Value).ToList(),
      change.Opened.Select(o => o.Id.Value).ToList()));
  }
}
