public class RejectDisposalHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit)
  : ICommandHandler<RejectDisposalCommand, Result<DisposalDecisionResult>>
{
  public async Task<Result<DisposalDecisionResult>> Handle(RejectDisposalCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    var disposal = await DisposalChecks.WaitingAsync(context, assetId, cancellationToken, track: true)
      ?? throw new AssetDisposalNotFoundException("There is no proposed disposal of this asset to reject.");

    disposal.Reject(currentUser.Actor(), command.Reason, clock.UtcNow);
    audit.Record(AssetAuditActions.Rejected, disposal, AssetAudit.Join($"Reason: {command.Reason.Trim()}", $"Proposed {DisposalChecks.ByWhom(disposal)}"));
    await context.SaveChangesAsync(cancellationToken);

    return Result<DisposalDecisionResult>.Success(new DisposalDecisionResult(true));
  }
}
