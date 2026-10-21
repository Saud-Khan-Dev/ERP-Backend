using Microsoft.EntityFrameworkCore;

public class ReverseDepreciationEntryHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<ReverseDepreciationEntryCommand, Result<ReverseDepreciationEntryCommandResult>>
{
  public async Task<Result<ReverseDepreciationEntryCommandResult>> Handle(ReverseDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    schedule.ReverseEntry(AssetDepreciationEntryId.Of(command.EntryId), currentUser.Actor(), clock.UtcNow);
    await context.SaveChangesAsync(cancellationToken);

    return Result<ReverseDepreciationEntryCommandResult>.Success(new ReverseDepreciationEntryCommandResult(true));
  }
}
