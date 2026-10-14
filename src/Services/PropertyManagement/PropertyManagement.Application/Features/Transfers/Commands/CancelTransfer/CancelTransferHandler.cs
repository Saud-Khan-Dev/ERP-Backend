public class CancelTransferHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<CancelTransferCommand, Result<CancelTransferCommandResult>>
{
  public async Task<Result<CancelTransferCommandResult>> Handle(CancelTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);

    // the route needs PROPERTY.EDIT; undoing an officer's approval needs the approver's permission
    if (transfer.TransferStatus == TransferStatus.Approved && !currentUser.HasPermission(PermissionCatalog.Property.Approve))
      throw new PermissionRequiredException(
        $"{transfer.TransferNo.Value} has been approved; cancelling it needs the Approve permission (PROPERTY.APPROVE).");

    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    transfer.Cancel(property, command.Reason, TransferRegistration.ActorName(currentUser), EntityLoaders.Today);

    await context.SaveChangesAsync(cancellationToken);
    return Result<CancelTransferCommandResult>.Success(new CancelTransferCommandResult(transfer.TransferStatus));
  }
}
