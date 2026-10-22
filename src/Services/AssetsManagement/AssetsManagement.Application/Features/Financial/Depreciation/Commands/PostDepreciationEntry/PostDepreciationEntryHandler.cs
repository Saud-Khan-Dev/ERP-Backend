using Microsoft.EntityFrameworkCore;

public class PostDepreciationEntryHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit)
  : ICommandHandler<PostDepreciationEntryCommand, Result<PostDepreciationEntryCommandResult>>
{
  public async Task<Result<PostDepreciationEntryCommandResult>> Handle(PostDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    var asset = await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    var entryId = AssetDepreciationEntryId.Of(command.EntryId);
    schedule.PostEntry(entryId, currentUser.Actor(), clock.UtcNow, clock.Today);
    var entry = schedule.Entry(entryId);
    audit.Line(AssetAuditActions.Posted, AssetAuditKinds.DepreciationMonth, asset, entry.Id.Value, AssetAudit.Month(entry),
      detail: AssetAudit.Join(AssetAudit.Period(entry), $"Amount {AssetAudit.Amount(entry.DepreciationAmount)}", $"Book value after {AssetAudit.Amount(entry.BookValueAfter)}"));
    await context.SaveChangesAsync(cancellationToken);

    return Result<PostDepreciationEntryCommandResult>.Success(new PostDepreciationEntryCommandResult(true));
  }
}
