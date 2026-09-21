using Microsoft.EntityFrameworkCore;

public class PostDepreciationEntryHandler(IApplicationDbContext context)
  : ICommandHandler<PostDepreciationEntryCommand, Result<PostDepreciationEntryCommandResult>>
{
  public async Task<Result<PostDepreciationEntryCommandResult>> Handle(PostDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    schedule.PostEntry(AssetDepreciationEntryId.Of(command.EntryId), command.PostedBy, DateTime.UtcNow);
    await context.SaveChangesAsync(cancellationToken);

    return Result<PostDepreciationEntryCommandResult>.Success(new PostDepreciationEntryCommandResult(true));
  }
}
