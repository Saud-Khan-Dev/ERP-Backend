using Microsoft.EntityFrameworkCore;

public class RunDepreciationHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock, IAuditTrail audit)
  : ICommandHandler<RunDepreciationCommand, Result<RunDepreciationCommandResult>>
{
  public async Task<Result<RunDepreciationCommandResult>> Handle(RunDepreciationCommand command, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var until = command.Until ?? today;
    var now = clock.UtcNow;

    if (command.AssetId is null)
      FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Run,
        "Running depreciation for every asset at once needs the Asset finance right to run depreciation. Run it from one asset's record instead.");
    else
      FinanceRights.RequireAny(currentUser, "Writing depreciation months needs the Asset finance right to record or to run depreciation.",
        PermissionCatalog.AssetFinance.Run, PermissionCatalog.AssetFinance.Create);

    if (command.Post)
    {
      FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Post, "Posting depreciation (making the months final) needs the Asset finance right to post. Write the months as drafts instead.");
      if (until > today)
        return Result<RunDepreciationCommandResult>.Failure("Months can only be posted once they are over. Choose a date up to today.");
    }

    Asset? onlyAsset = null;
    var active = context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.IsActive);
    if (command.AssetId.HasValue)
    {
      var only = AssetId.Of(command.AssetId.Value);
      onlyAsset = await AssetRecordGuard.LoadAsync(context, only, track: false, cancellationToken);
      active = active.Where(s => s.AssetId == only);
    }
    var schedules = await active.ToListAsync(cancellationToken);
    var methods = await context.DepreciationMethods.AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
    var assetIds = schedules.Select(s => s.AssetId).Distinct().ToList();
    var acquisitions = await context.AssetAcquisitions.AsNoTracking().Where(a => assetIds.Contains(a.AssetId)).ToDictionaryAsync(a => a.AssetId, cancellationToken);
    // the query filter leaves deleted assets out
    var assets = await context.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
    var disposed = (await context.AssetDisposals.AsNoTracking().Where(d => assetIds.Contains(d.AssetId) && d.State == DisposalState.Approved)
      .Select(d => d.AssetId).ToListAsync(cancellationToken)).ToHashSet();
    var terminal = (await context.AssetStatuses.AsNoTracking().Where(s => s.IsTerminal).Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();

    var actor = currentUser.Actor();
    var skipped = new List<DepreciationRunSkip>();
    var generated = 0;
    var posted = 0;
    var amount = 0m;
    var counted = 0;

    foreach (var schedule in schedules)
    {
      if (!assets.TryGetValue(schedule.AssetId, out var asset))
        continue; // deleted asset

      var code = asset.AssetCode.Value;
      if (disposed.Contains(asset.Id) || terminal.Contains(asset.StatusId))
      {
        skipped.Add(new DepreciationRunSkip(asset.Id.Value, code, "Its record is closed (disposed)."));
        continue;
      }

      if (!acquisitions.ContainsKey(schedule.AssetId))
      {
        skipped.Add(new DepreciationRunSkip(asset.Id.Value, code, "No purchase cost is recorded."));
        continue;
      }

      counted++;
      var method = methods[schedule.MethodId];
      try
      {
        if (method.Kind != DepreciationMethodKind.UnitsOfProduction)
        {
          var entries = schedule.GenerateEntries(method, until);
          generated += entries.Count;
          amount += entries.Sum(e => e.DepreciationAmount);
        }
        else
        {
          skipped.Add(new DepreciationRunSkip(asset.Id.Value, code, "Units-of-production months are entered by hand."));
        }

        if (command.Post)
          posted += schedule.PostEndedEntries(until, actor, now, today);
      }
      catch (DomainException error)
      {
        skipped.Add(new DepreciationRunSkip(asset.Id.Value, code, error.Message));
      }
    }

    // one line for the whole run (who, up to when, what it did); the months themselves are not written one by one
    var label = $"Up to {AssetAudit.Day(until)}{(command.Post ? ", posted" : ", as drafts")}";
    var detail = AssetAudit.Join(
      $"{counted} {(counted == 1 ? "schedule" : "schedules")}",
      $"{generated} {(generated == 1 ? "month" : "months")} written ({AssetAudit.Amount(amount)})",
      command.Post ? $"{posted} posted" : null,
      skipped.Count > 0 ? $"{skipped.Count} skipped: {string.Join("; ", skipped.Take(5).Select(s => $"{s.AssetCode} ({s.Reason})"))}{(skipped.Count > 5 ? " …" : "")}" : null);
    if (onlyAsset is not null)
      audit.Line(AuditActions.Ran, AssetAuditKinds.DepreciationRun, onlyAsset, Guid.CreateVersion7(), recordLabel: label, detail: detail);
    else
      audit.Record(AuditActions.Ran, AssetAuditKinds.DepreciationRun, Guid.CreateVersion7(), null, label, null, null, detail);

    using (audit.Quietly())
      await context.SaveChangesAsync(cancellationToken);

    return Result<RunDepreciationCommandResult>.Success(new RunDepreciationCommandResult(counted, generated, posted, amount, skipped));
  }
}
