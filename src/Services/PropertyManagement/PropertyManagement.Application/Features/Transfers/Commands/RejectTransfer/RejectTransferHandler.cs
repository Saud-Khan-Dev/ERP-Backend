public class RejectTransferHandler(IApplicationDbContext context, ICurrentUser currentUser, IAuditTrail audit)
  : ICommandHandler<RejectTransferCommand, Result<RejectTransferCommandResult>>
{
  public async Task<Result<RejectTransferCommandResult>> Handle(RejectTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    var property = await context.LoadPropertyAsync(transfer.PropertyId.Value, cancellationToken);
    // closing a waiting transfer frees the givers' shares: it must not cross an approval of the same property
    context.Touch(property);

    transfer.Reject(property, command.Reason, TransferRegistration.ActorName(currentUser), EntityLoaders.Today);

    audit.Record(PropertyAuditActions.Rejected, transfer, command.Reason);
    await context.SaveChangesAsync(cancellationToken);
    return Result<RejectTransferCommandResult>.Success(new RejectTransferCommandResult(transfer.TransferStatus));
  }
}
