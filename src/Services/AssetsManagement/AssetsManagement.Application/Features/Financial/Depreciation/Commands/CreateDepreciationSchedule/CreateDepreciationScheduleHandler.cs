using Microsoft.EntityFrameworkCore;

public class CreateDepreciationScheduleHandler(IApplicationDbContext context)
  : ICommandHandler<CreateDepreciationScheduleCommand, Result<CreateDepreciationScheduleCommandResult>>
{
  public async Task<Result<CreateDepreciationScheduleCommandResult>> Handle(CreateDepreciationScheduleCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await context.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken)
      ?? throw new AssetNotFoundException($"Asset {command.AssetId} was not found.");

    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    var methodId = DepreciationMethodId.Of(command.MethodId);
    var method = await context.DepreciationMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException($"Depreciation method {command.MethodId} was not found.");

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAcquisitionNotFoundException("Record the asset's acquisition (cost) before creating a depreciation schedule.");

    if (await context.AssetDepreciationSchedules.AnyAsync(s => s.AssetId == assetId && s.IsActive, cancellationToken))
      return Result<CreateDepreciationScheduleCommandResult>.Failure("This asset already has an active depreciation schedule. Deactivate it first.");

    var schedule = AssetDepreciationSchedule.Create(
      AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, assetType, method, acquisition.AcquisitionCost,
      command.UsefulLifeMonths, command.SalvageValue, command.DecliningRate, command.StartDate);

    await context.AssetDepreciationSchedules.AddAsync(schedule, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateDepreciationScheduleCommandResult>.Success(new CreateDepreciationScheduleCommandResult(schedule.Id.Value));
  }
}
