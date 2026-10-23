using Microsoft.EntityFrameworkCore;

public class DeleteDepreciationEntryHandler(IApplicationDbContext context, IAuditTrail audit)
  : ICommandHandler<DeleteDepreciationEntryCommand, Result<DeleteDepreciationEntryCommandResult>>
{
  public async Task<Result<DeleteDepreciationEntryCommandResult>> Handle(DeleteDepreciationEntryCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    var asset = await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    var entryId = AssetDepreciationEntryId.Of(command.EntryId);
    var entry = schedule.Entry(entryId);
    schedule.RemoveEntry(entryId);
    audit.Line(AuditActions.Removed, AssetAuditKinds.DepreciationMonth, asset, entry.Id.Value, AssetAudit.Month(entry),
      detail: AssetAudit.Join("Draft month", AssetAudit.Period(entry), $"Amount {AssetAudit.Amount(entry.DepreciationAmount)}"));
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeleteDepreciationEntryCommandResult>.Success(new DeleteDepreciationEntryCommandResult(true));
  }
}
