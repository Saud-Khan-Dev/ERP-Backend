using Microsoft.EntityFrameworkCore;

public class DeleteDepreciationEntryHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteDepreciationEntryCommand, Result<DeleteDepreciationEntryCommandResult>>
{
  public async Task<Result<DeleteDepreciationEntryCommandResult>> Handle(DeleteDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    schedule.RemoveEntry(AssetDepreciationEntryId.Of(command.EntryId));
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteDepreciationEntryCommandResult>.Success(new DeleteDepreciationEntryCommandResult(true));
  }
}
