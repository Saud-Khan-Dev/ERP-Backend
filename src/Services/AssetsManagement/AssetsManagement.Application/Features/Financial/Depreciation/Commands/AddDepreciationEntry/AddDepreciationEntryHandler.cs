using Microsoft.EntityFrameworkCore;

public class AddDepreciationEntryHandler(IApplicationDbContext context)
  : ICommandHandler<AddDepreciationEntryCommand, Result<AddDepreciationEntryCommandResult>>
{
  public async Task<Result<AddDepreciationEntryCommandResult>> Handle(AddDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == schedule.AssetId, cancellationToken)
      ?? throw new AssetAcquisitionNotFoundException("The asset has no acquisition record.");

    var entry = schedule.AddManualEntry(command.PeriodStart, command.PeriodEnd, command.DepreciationAmount, acquisition.AcquisitionCost);

    await context.SaveChangesAsync(cancellationToken);

    return Result<AddDepreciationEntryCommandResult>.Success(new AddDepreciationEntryCommandResult(entry.Id.Value));
  }
}
