using Microsoft.EntityFrameworkCore;

public class CreateDepreciationScheduleHandler(IApplicationDbContext context, IClock clock)
  : ICommandHandler<CreateDepreciationScheduleCommand, Result<CreateDepreciationScheduleCommandResult>>
{
  public async Task<Result<CreateDepreciationScheduleCommandResult>> Handle(CreateDepreciationScheduleCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    var methodId = DepreciationMethodId.Of(command.MethodId);
    var method = await context.DepreciationMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException("The depreciation method was not found. Choose it again.");

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken)
      ?? throw new AssetAcquisitionNotFoundException("Record the asset's purchase (its cost) before starting depreciation.");

    var schedules = await context.AssetDepreciationSchedules.AsNoTracking().Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);
    if (schedules.Any(s => s.IsActive))
      return Result<CreateDepreciationScheduleCommandResult>.Failure("This asset already has a running depreciation schedule. Revise it instead.");

    if (command.StartDate < acquisition.AcquisitionDate)
      return Result<CreateDepreciationScheduleCommandResult>.Failure($"Depreciation cannot start before the asset was acquired ({acquisition.AcquisitionDate:dd'/'MM'/'yyyy}).");

    if (AssetBookValue.PostedThrough(schedules) is { } posted && command.StartDate <= posted)
      return Result<CreateDepreciationScheduleCommandResult>.Failure($"Months are already posted up to {posted:dd'/'MM'/'yyyy}. The new schedule must start after them.");

    // the base is what is left of the cost after every posted month - never the full cost again
    var bookValue = AssetBookValue.NetBookValue(acquisition.AcquisitionCost, schedules, DateOnly.MaxValue);

    var schedule = AssetDepreciationSchedule.Create(
      AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, assetType, method, bookValue,
      command.UsefulLifeMonths, command.SalvageValue, command.DecliningRate, command.StartDate);

    await context.AssetDepreciationSchedules.AddAsync(schedule, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<CreateDepreciationScheduleCommandResult>.Success(new CreateDepreciationScheduleCommandResult(schedule.Id.Value));
  }
}
