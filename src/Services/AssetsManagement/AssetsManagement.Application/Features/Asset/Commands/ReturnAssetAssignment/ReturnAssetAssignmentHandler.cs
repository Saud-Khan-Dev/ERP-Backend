using Microsoft.EntityFrameworkCore;

public class ReturnAssetAssignmentHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<ReturnAssetAssignmentCommand, Result<ReturnAssetAssignmentCommandResult>>
{
  public async Task<Result<ReturnAssetAssignmentCommandResult>> Handle(ReturnAssetAssignmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);

    var assignmentId = AssetAssignmentId.Of(command.AssignmentId);
    var assignment = await context.AssetAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAssignmentNotFoundException("That loan was not found on this asset.");

    var today = clock.Today;
    var returnedOn = command.ActualReturnDate ?? today;
    assignment.MarkReturned(returnedOn, today);

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.ReturnFromLoan(currentStatus, assignment.From, assignment.FromLocationId);

    // the day it came back, at the time it is recorded (today) - or the end of that day for an earlier return
    var at = returnedOn == today ? clock.UtcNow : returnedOn.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    await SystemEvents.AddAsync(context, assetId, EventPurpose.Return, at, currentUser.Actor(), command.Notes ?? "Returned from loan", cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<ReturnAssetAssignmentCommandResult>.Success(new ReturnAssetAssignmentCommandResult(true));
  }
}
