using Microsoft.EntityFrameworkCore;

public class RunDepreciationHandler(IApplicationDbContext context)
  : ICommandHandler<RunDepreciationCommand, Result<RunDepreciationCommandResult>>
{
  public async Task<Result<RunDepreciationCommandResult>> Handle(RunDepreciationCommand command, CancellationToken cancellationToken)
  {
    var until = command.Until ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var now = DateTime.UtcNow;

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
    var assets = await context.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);

    var skipped = new List<DepreciationRunSkip>();
    var generated = 0;
    var posted = 0;
    var amount = 0m;

    foreach (var schedule in schedules)
    {
      var code = assets.TryGetValue(schedule.AssetId, out var asset) ? asset.AssetCode.Value : "";
      if (asset is null)
        continue; // deleted asset

      if (!acquisitions.TryGetValue(schedule.AssetId, out var acquisition))
      {
        skipped.Add(new DepreciationRunSkip(schedule.AssetId.Value, code, "No purchase cost is recorded."));
        continue;
      }

      var method = methods[schedule.MethodId];
      try
      {
        if (method.Code.Value != DepreciationMethod.UnitsOfProduction)
        {
          var entries = schedule.GenerateEntries(method, until, acquisition.AcquisitionCost);
          generated += entries.Count;
          amount += entries.Sum(e => e.DepreciationAmount);
        }
        else
        {
          skipped.Add(new DepreciationRunSkip(schedule.AssetId.Value, code, "Units-of-production entries are entered by hand."));
        }

        if (command.Post)
        {
          foreach (var entry in schedule.Entries.Where(e => !e.Posted && !e.IsReversed && e.PeriodEnd <= until.AddDays(1)).ToList())
          {
            schedule.PostEntry(entry.Id, command.PostedBy, now);
            posted++;
          }
        }
      }
      catch (DomainException error)
      {
        skipped.Add(new DepreciationRunSkip(schedule.AssetId.Value, code, error.Message));
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result<RunDepreciationCommandResult>.Success(new RunDepreciationCommandResult(schedules.Count, generated, posted, amount, skipped));
  }
}
