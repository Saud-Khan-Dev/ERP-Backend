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

    await context.SaveChangesAsync(cancellationToken);

    return Result<ReturnAssetAssignmentCommandResult>.Success(new ReturnAssetAssignmentCommandResult(true));
  }
}
