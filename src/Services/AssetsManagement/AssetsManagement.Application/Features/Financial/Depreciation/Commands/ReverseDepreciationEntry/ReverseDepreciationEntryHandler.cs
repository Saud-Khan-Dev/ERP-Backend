using Microsoft.EntityFrameworkCore;

public class ReverseDepreciationEntryHandler(IApplicationDbContext context)
  : ICommandHandler<ReverseDepreciationEntryCommand, Result<ReverseDepreciationEntryCommandResult>>
{
  public async Task<Result<ReverseDepreciationEntryCommandResult>> Handle(ReverseDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    schedule.ReverseEntry(AssetDepreciationEntryId.Of(command.EntryId), command.ReversedBy, DateTime.UtcNow);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ReverseDepreciationEntryCommandResult>.Success(new ReverseDepreciationEntryCommandResult(true));
  }
}
