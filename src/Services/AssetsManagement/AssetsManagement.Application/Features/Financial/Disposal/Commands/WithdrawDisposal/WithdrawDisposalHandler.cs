public class WithdrawDisposalHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit)
  : ICommandHandler<WithdrawDisposalCommand, Result<DisposalDecisionResult>>
{
  public async Task<Result<DisposalDecisionResult>> Handle(WithdrawDisposalCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    var disposal = await DisposalChecks.WaitingAsync(context, assetId, cancellationToken, track: true)
      ?? throw new AssetDisposalNotFoundException("There is no proposed disposal of this asset to withdraw.");

    disposal.Withdraw(currentUser.Actor(), command.Reason, clock.UtcNow);
    audit.Record(AssetAuditActions.Withdrawn, disposal, string.IsNullOrWhiteSpace(command.Reason) ? null : $"Reason: {command.Reason.Trim()}");
    await context.SaveChangesAsync(cancellationToken);

    return Result<DisposalDecisionResult>.Success(new DisposalDecisionResult(true));
  }
}
