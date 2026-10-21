using Microsoft.EntityFrameworkCore;

public class RunDepreciationHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<RunDepreciationCommand, Result<RunDepreciationCommandResult>>
{
  public async Task<Result<RunDepreciationCommandResult>> Handle(RunDepreciationCommand command, CancellationToken cancellationToken)
  {
    var today = clock.Today;
    var until = command.Until ?? today;
    var now = clock.UtcNow;

    if (command.Post)
    {
      FinanceRights.Require(currentUser, PermissionCatalog.AssetFinance.Post, "Posting depreciation (making the months final) needs the Asset finance right to post. Write the months as drafts instead.");
      if (until > today)
        return Result<RunDepreciationCommandResult>.Failure("Months can only be posted once they are over. Choose a date up to today.");
    }

    var active = context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.IsActive);
    if (command.AssetId.HasValue)
    {
      var only = AssetId.Of(command.AssetId.Value);
      active = active.Where(s => s.AssetId == only);
    }
    var schedules = await active.ToListAsync(cancellationToken);
    var methods = await context.DepreciationMethods.AsNoTracking().ToDictionaryAsync(m => m.Id, cancellationToken);
    var assetIds = schedules.Select(s => s.AssetId).Distinct().ToList();
    var acquisitions = await context.AssetAcquisitions.AsNoTracking().Where(a => assetIds.Contains(a.AssetId)).ToDictionaryAsync(a => a.AssetId, cancellationToken);
    // the query filter leaves deleted assets out
    var assets = await context.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
    var disposed = (await context.AssetDisposals.AsNoTracking().Where(d => assetIds.Contains(d.AssetId)).Select(d => d.AssetId).ToListAsync(cancellationToken)).ToHashSet();
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

    await context.SaveChangesAsync(cancellationToken);

    return Result<RunDepreciationCommandResult>.Success(new RunDepreciationCommandResult(counted, generated, posted, amount, skipped));
  }
}
