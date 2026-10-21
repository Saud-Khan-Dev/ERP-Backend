using Microsoft.EntityFrameworkCore;

public class TransferAssetHandler(IApplicationDbContext context, IHrmDirectory hrm, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<TransferAssetCommand, Result<TransferAssetCommandResult>>
{
  public async Task<Result<TransferAssetCommandResult>> Handle(TransferAssetCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var currentStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    if (await context.AssetAssignments.AnyAsync(a => a.AssetId == assetId && a.ExpectedReturnDate != null && a.ActualReturnDate == null, cancellationToken))
      return Result<TransferAssetCommandResult>.Failure("This asset is out on loan. Record its return before moving it again.");

    LocationId? toLocationId = asset.CurrentLocationId;
    if (command.ToLocationId.HasValue)
    {
      toLocationId = LocationId.Of(command.ToLocationId.Value);
      if (!await context.Locations.AnyAsync(l => l.Id == toLocationId && l.IsActive, cancellationToken))
        throw new LocationNotFoundException("The location was not found or is switched off. Choose it again.");
    }

    // the person and the unit as HR has them today; a new person brings their unit unless one is chosen
    var current = asset.Custody;
    var chosen = command.ToCustodianId.HasValue || command.ToDepartmentId.HasValue
      ? await CustodyResolver.ResolveAsync(hrm, command.ToCustodianId, command.ToDepartmentId, cancellationToken)
      : CustodyRef.None;

    // custodian: the new person / nobody / as now
    var (custodianId, custodianName, custodianNumber) =
      command.ToCustodianId.HasValue ? (chosen.CustodianId, chosen.CustodianName, chosen.CustodianNumber)
      : command.ClearCustodian ? ((Guid?)null, (string?)null, (string?)null)
      : (current.CustodianId, current.CustodianName, current.CustodianNumber);

    // department: the one chosen / the new person's unit (when HR knows it) / none / as now
    var takesUnit = command.ToDepartmentId.HasValue || (command.ToCustodianId.HasValue && chosen.DepartmentId is not null);
    var (departmentId, departmentName) =
      takesUnit ? (chosen.DepartmentId, chosen.DepartmentName)
      : command.ClearDepartment ? ((Guid?)null, (string?)null)
      : (current.DepartmentId, current.DepartmentName);

    var target = new CustodyRef(departmentId, departmentName, custodianId, custodianName, custodianNumber);

    var purchaseDate = await context.AssetAcquisitions.AsNoTracking().Where(a => a.AssetId == assetId).Select(a => (DateOnly?)a.AcquisitionDate).FirstOrDefaultAsync(cancellationToken);
    var assignmentDate = EventDates.Check(command.AssignmentDate, clock, asset, purchaseDate);
    var actor = currentUser.Actor();

    var assignment = AssetAssignment.Create(
      AssetAssignmentId.Of(Guid.NewGuid()), asset, target, toLocationId, assignmentDate, command.ExpectedReturnDate, command.Reason, actor, clock.UtcNow);

    asset.AssignTo(currentStatus, assetType, target, toLocationId);

    await context.AssetAssignments.AddAsync(assignment, cancellationToken);
    // the move and its timeline line share one instant, so the timeline shows the move once
    await SystemEvents.AddAsync(context, assetId, command.ExpectedReturnDate.HasValue ? EventPurpose.Loan : EventPurpose.Transfer,
      assignmentDate, actor, command.Reason, cancellationToken: cancellationToken);

    await context.SaveChangesAsync(cancellationToken);

    return Result<TransferAssetCommandResult>.Success(new TransferAssetCommandResult(assignment.Id.Value));
  }
}
