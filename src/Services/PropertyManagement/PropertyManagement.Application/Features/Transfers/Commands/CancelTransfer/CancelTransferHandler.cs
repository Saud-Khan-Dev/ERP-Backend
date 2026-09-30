public class CancelTransferHandler(IApplicationDbContext context)
  : ICommandHandler<CancelTransferCommand, Result<CancelTransferCommandResult>>
{
  public async Task<Result<CancelTransferCommandResult>> Handle(CancelTransferCommand command, CancellationToken cancellationToken)
  {
    var transfer = await context.LoadTransferAsync(command.Id, cancellationToken);
    transfer.Cancel(command.Reason);

    await context.SaveChangesAsync(cancellationToken);
    return Result<CancelTransferCommandResult>.Success(new CancelTransferCommandResult(transfer.TransferStatus));
  }
}
