using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class CancelDisposalHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit)
  : ICommandHandler<CancelDisposalCommand, Result<CancelDisposalCommandResult>>
{
  public async Task<Result<CancelDisposalCommandResult>> Handle(CancelDisposalCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);

    var disposal = await context.AssetDisposals.FirstOrDefaultAsync(d => d.AssetId == assetId && d.State == DisposalState.Approved, cancellationToken)
      ?? throw new AssetDisposalNotFoundException("This asset has not been disposed of. A proposal waiting for approval is rejected or withdrawn instead.");

    // the status it had before: the "from" side of its disposal event
    var disposalType = await SystemEvents.TypeAsync(context, EventPurpose.Disposal, cancellationToken);
    var disposedEvent = await context.AssetLifecycleEvents.AsNoTracking()
      .Where(e => e.AssetId == assetId && e.EventTypeId == disposalType.Id && e.FromStatusId != null)
      .OrderByDescending(e => e.EventDate)
      .FirstOrDefaultAsync(cancellationToken);
    var restoreId = disposedEvent?.FromStatusId;
    var restoreTo = restoreId is null ? null : await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == restoreId, cancellationToken);
    restoreTo ??= await context.AssetStatuses.AsNoTracking().Where(s => s.IsActive && !s.IsTerminal).OrderBy(s => s.DisplayOrder).FirstOrDefaultAsync(cancellationToken);
    if (restoreTo is null)
      return Result<CancelDisposalCommandResult>.Failure("There is no status the asset could go back to. Add one in Assets › Configuration › Statuses first.");

    var current = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.CancelDisposal(current, restoreTo);

    // the schedule the disposal stopped (it ended on the disposal date) runs again
    var schedules = await context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);
    if (!schedules.Any(s => s.IsActive))
    {
      var stopped = schedules.Where(s => s.EndDate == disposal.DisposalDate.AddDays(1)).OrderByDescending(s => s.StartDate).FirstOrDefault();
      stopped?.Reopen();
    }

    var actor = currentUser.Actor();
    var method = await context.DisposalMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == disposal.DisposalMethodId, cancellationToken);
    var details = JsonSerializer.SerializeToElement(new Dictionary<string, string>
    {
      ["Was disposed on"] = disposal.DisposalDate.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture),
      ["How"] = method?.Name.Value ?? "—",
    });
    await SystemEvents.AddAsync(context, assetId, EventPurpose.DisposalCancelled, clock.UtcNow, actor, command.Reason, details,
      current.Id, restoreTo.Id, cancellationToken);

    // the disposal stays on record, marked cancelled - with its proceeds, book value, gain or loss and approver
    disposal.Cancel(actor, command.Reason, clock.UtcNow);
    audit.Record(AssetAuditActions.Cancelled, disposal, AssetAudit.Join($"Reason: {command.Reason.Trim()}",
      $"Was disposed on {AssetAudit.Day(disposal.DisposalDate)}", disposal.ApprovedByName is { } approver ? $"Approved by {approver}" : null));
    await context.SaveChangesAsync(cancellationToken);

    return Result<CancelDisposalCommandResult>.Success(new CancelDisposalCommandResult(true));
  }
}
