using Microsoft.EntityFrameworkCore;

public class PostDepreciationEntryHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<PostDepreciationEntryCommand, Result<PostDepreciationEntryCommandResult>>
{
  public async Task<Result<PostDepreciationEntryCommandResult>> Handle(PostDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    schedule.PostEntry(AssetDepreciationEntryId.Of(command.EntryId), currentUser.Actor(), clock.UtcNow, clock.Today);
    await context.SaveChangesAsync(cancellationToken);

    return Result<PostDepreciationEntryCommandResult>.Success(new PostDepreciationEntryCommandResult(true));
  }
}
