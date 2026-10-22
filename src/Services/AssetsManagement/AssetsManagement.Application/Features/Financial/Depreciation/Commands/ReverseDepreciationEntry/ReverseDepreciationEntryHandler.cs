using Microsoft.EntityFrameworkCore;

public class ReverseDepreciationEntryHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit, IDutyRules dutyRules)
  : ICommandHandler<ReverseDepreciationEntryCommand, Result<ReverseDepreciationEntryCommandResult>>
{
  public async Task<Result<ReverseDepreciationEntryCommandResult>> Handle(ReverseDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    var asset = await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    var entryId = AssetDepreciationEntryId.Of(command.EntryId);
    var entry = schedule.Entry(entryId);

    // maker-checker: the person who posted a month does not reverse it (checked before anything changes)
    var refusal = await dutyRules.SeparationRefusalAsync(DutyRules.DepreciationReverse, entry.PostedBy, currentUser.UserId,
      "reverse a depreciation month you posted", cancellationToken);
    if (refusal is not null)
      return Result<ReverseDepreciationEntryCommandResult>.Failure(refusal);

    schedule.ReverseEntry(entryId, currentUser.Actor(), clock.UtcNow, command.Reason);
    audit.Line(AssetAuditActions.Reversed, AssetAuditKinds.DepreciationMonth, asset, entry.Id.Value, AssetAudit.Month(entry),
      detail: AssetAudit.Join($"Reason: {command.Reason.Trim()}", AssetAudit.Period(entry), $"Amount {AssetAudit.Amount(entry.DepreciationAmount)}",
        entry.PostedByName is { } poster ? $"Posted by {poster}" : null));
    await context.SaveChangesAsync(cancellationToken);

    return Result<ReverseDepreciationEntryCommandResult>.Success(new ReverseDepreciationEntryCommandResult(true));
  }
}
