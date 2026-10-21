using Microsoft.EntityFrameworkCore;

public class AddDepreciationEntryHandler(IApplicationDbContext context)
  : ICommandHandler<AddDepreciationEntryCommand, Result<AddDepreciationEntryCommandResult>>
{
  public async Task<Result<AddDepreciationEntryCommandResult>> Handle(AddDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    // stored with the day after the period, like every generated month
    var entry = schedule.AddManualEntry(command.PeriodStart, command.PeriodEnd.AddDays(1), command.DepreciationAmount);

    await context.SaveChangesAsync(cancellationToken);

    return Result<AddDepreciationEntryCommandResult>.Success(new AddDepreciationEntryCommandResult(entry.Id.Value));
  }
}
