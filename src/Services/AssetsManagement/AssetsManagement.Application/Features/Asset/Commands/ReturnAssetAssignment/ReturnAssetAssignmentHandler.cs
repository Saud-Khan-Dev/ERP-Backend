using Microsoft.EntityFrameworkCore;

public class ReturnAssetAssignmentHandler(IApplicationDbContext context)
  : ICommandHandler<ReturnAssetAssignmentCommand, Result<ReturnAssetAssignmentCommandResult>>
{
  public async Task<Result<ReturnAssetAssignmentCommandResult>> Handle(ReturnAssetAssignmentCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var assignmentId = AssetAssignmentId.Of(command.AssignmentId);
    var assignment = await context.AssetAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAssignmentNotFoundException($"Assignment {command.AssignmentId} was not found on asset {command.AssetId}.");

    assignment.MarkReturned(command.ActualReturnDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    asset.AssignTo(currentStatus, assetType, assignment.FromDepartmentId, assignment.FromCustodianId, assignment.FromLocationId);

    if (command.EventTypeId.HasValue)
    {
      var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId.Value);
      var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
        ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.EventTypeId} was not found.");

      var returnedOn = command.ActualReturnDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
      await context.AssetLifecycleEvents.AddAsync(AssetLifecycleEvent.Create(
        AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, eventType,
        returnedOn.ToDateTime(TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc),
        null, null, command.PerformedBy, command.Notes ?? "Returned from loan", null), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<ReturnAssetAssignmentCommandResult>.Success(new ReturnAssetAssignmentCommandResult(true));
  }
}
