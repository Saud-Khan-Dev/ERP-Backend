public class ApproveTransferHandler(IApplicationDbContext context, ICurrentUser currentUser)
  : ICommandHandler<ApproveTransferCommand, Result<ApproveTransferCommandResult>>
{
  public async Task<Result<ApproveTransferCommandResult>> Handle(ApproveTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);

    transfer.Approve(
      command.ApprovedBy ?? currentUser.Username ?? currentUser.AuditName,
      command.ApprovalDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

    await context.SaveChangesAsync(cancellationToken);
    return Result<ApproveTransferCommandResult>.Success(new ApproveTransferCommandResult(transfer.TransferStatus));
  }
}
