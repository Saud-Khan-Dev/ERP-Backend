using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class ApproveDisposalHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit, IDutyRules dutyRules)
  : ICommandHandler<ApproveDisposalCommand, Result<ApproveDisposalCommandResult>>
{
  public async Task<Result<ApproveDisposalCommandResult>> Handle(ApproveDisposalCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    var disposal = await DisposalChecks.WaitingAsync(context, assetId, cancellationToken, track: true)
      ?? throw new AssetDisposalNotFoundException("There is no proposed disposal of this asset to approve.");

    // maker-checker, before anything changes
    var refusal = await dutyRules.SeparationRefusalAsync(DutyRules.AssetDisposal, disposal.ProposedBy, currentUser.UserId,
      "approve a disposal you proposed", cancellationToken);
    if (refusal is not null)
      return Result<ApproveDisposalCommandResult>.Failure(refusal);

    if (await DisposalChecks.OnLoanAsync(context, assetId, cancellationToken))
      return Result<ApproveDisposalCommandResult>.Failure("This asset is out on loan. Record its return before approving its disposal.");

    var method = await context.DisposalMethods.AsNoTracking().FirstAsync(m => m.Id == disposal.DisposalMethodId, cancellationToken);
    var toStatus = disposal.ToStatusId is { } toStatusId
      ? await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == toStatusId, cancellationToken)
      : throw new DomainException("The proposal does not say which final status the asset takes. Reject it and propose it again.");

    var today = clock.Today;
    var now = clock.UtcNow;
    var actor = currentUser.Actor();
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var schedules = await context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);

    // months posted after the disposal date would be depreciation of an asset GDA no longer had
    if (AssetBookValue.PostedThrough(schedules) is { } posted && posted > disposal.DisposalDate)
      return Result<ApproveDisposalCommandResult>.Failure($"Depreciation is posted up to {posted:d MMM yyyy}, after the disposal date. Reverse those months first, or reject the proposal and propose a later date.");

    // depreciate up to the disposal date first, so the book value at disposal is right
    var monthsPosted = 0;
    foreach (var schedule in schedules.Where(s => s.IsActive))
    {
      var scheduleMethod = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
      if (scheduleMethod.Kind != DepreciationMethodKind.UnitsOfProduction && acquisition is not null)
        schedule.GenerateEntries(scheduleMethod, disposal.DisposalDate);
      monthsPosted += schedule.PostEndedEntries(disposal.DisposalDate, actor, now, today);
    }

    var bookValue = acquisition is null ? (decimal?)null : AssetBookValue.NetBookValue(acquisition.AcquisitionCost, schedules, disposal.DisposalDate);
    disposal.Approve(method, toStatus, bookValue, acquisition?.CurrencyCode, acquisition?.AcquisitionDate, actor, now, today);

    // the schedule stops on the disposal date; draft months after it go
    foreach (var schedule in schedules.Where(s => s.IsActive))
      schedule.Close(disposal.DisposalDate.AddDays(1));

    var fromStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.Dispose(fromStatus, toStatus);

    // stamped with the time it was approved, so it sorts after everything else of that day
    var eventDate = disposal.DisposalDate == today ? now : disposal.DisposalDate.ToDateTime(TimeOnly.FromDateTime(now), DateTimeKind.Utc);
    var details = new Dictionary<string, string> { ["How"] = method.Name.Value };
    if (disposal.DisposalValue is { } proceeds) details["Proceeds"] = $"{proceeds.ToString("N2", CultureInfo.InvariantCulture)} {disposal.CurrencyCode?.Value}";
    if (bookValue is { } nbv) details["Book value"] = nbv.ToString("N2", CultureInfo.InvariantCulture);
    if (disposal.ProposedByName is { } proposer) details["Proposed by"] = proposer;
    await SystemEvents.AddAsync(context, assetId, EventPurpose.Disposal, eventDate, actor, disposal.Reason, JsonSerializer.SerializeToElement(details),
      fromStatus.Id, toStatus.Id, cancellationToken);

    audit.Record(AssetAuditActions.Approved, disposal, AssetAudit.Join(
      $"Proposed {DisposalChecks.ByWhom(disposal)}",
      $"Disposed on {AssetAudit.Day(disposal.DisposalDate)}",
      bookValue is { } value ? $"Book value {AssetAudit.Amount(value)}" : "No purchase recorded",
      disposal.GainLoss is { } gain ? $"{(gain >= 0 ? "Gain" : "Loss")} {AssetAudit.Amount(Math.Abs(gain))}" : null,
      monthsPosted > 0 ? $"{monthsPosted} {(monthsPosted == 1 ? "month" : "months")} posted first" : null,
      command.Note));

    await context.SaveChangesAsync(cancellationToken);

    return Result<ApproveDisposalCommandResult>.Success(new ApproveDisposalCommandResult(disposal.Id.Value, disposal.NetBookValueAtDisposal, disposal.GainLoss, monthsPosted));
  }
}
