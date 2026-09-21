using Microsoft.EntityFrameworkCore;

public class TransferAssetHandler(IApplicationDbContext context)
  : ICommandHandler<TransferAssetCommand, Result<TransferAssetCommandResult>>
{
  public async Task<Result<TransferAssetCommandResult>> Handle(TransferAssetCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    LocationId? toLocationId = asset.CurrentLocationId;
    if (command.ToLocationId.HasValue)
    {
      toLocationId = LocationId.Of(command.ToLocationId.Value);
      if (!await context.Locations.AnyAsync(l => l.Id == toLocationId && l.IsActive, cancellationToken))
        throw new LocationNotFoundException($"Location {command.ToLocationId} was not found or is inactive.");
    }

    var toDepartmentId = command.ToDepartmentId ?? asset.DepartmentId;
    var toCustodianId = command.ToCustodianId ?? asset.CustodianId;

    if (await context.AssetAssignments.AnyAsync(a => a.AssetId == assetId && a.ExpectedReturnDate != null && a.ActualReturnDate == null, cancellationToken))
      return Result<TransferAssetCommandResult>.Failure("This asset is out on a temporary assignment. Record its return before transferring it again.");

    var assignmentDate = command.AssignmentDate ?? DateTime.UtcNow;

    var assignment = AssetAssignment.Create(
      AssetAssignmentId.Of(Guid.NewGuid()), asset, toDepartmentId, toCustodianId, toLocationId,
      assignmentDate, command.ExpectedReturnDate, command.Reason, command.ApprovedBy, command.ApprovedAt);

    asset.AssignTo(currentStatus, assetType, toDepartmentId, toCustodianId, toLocationId);

    await context.AssetAssignments.AddAsync(assignment, cancellationToken);

    if (command.EventTypeId.HasValue)
    {
      var eventTypeId = LifecycleEventTypeId.Of(command.EventTypeId.Value);
      var eventType = await context.LifecycleEventTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eventTypeId, cancellationToken)
        ?? throw new LifecycleEventTypeNotFoundException($"Lifecycle event type {command.EventTypeId} was not found.");

      await context.AssetLifecycleEvents.AddAsync(AssetLifecycleEvent.Create(
        AssetLifecycleEventId.Of(Guid.NewGuid()), assetId, eventType, assignmentDate, null, null,
        command.PerformedBy, command.Reason, details: null), cancellationToken);
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<TransferAssetCommandResult>.Success(new TransferAssetCommandResult(assignment.Id.Value));
  }
}
