using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class DisposeAssetHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<DisposeAssetCommand, Result<DisposeAssetCommandResult>>
{
  public async Task<Result<DisposeAssetCommandResult>> Handle(DisposeAssetCommand command, CancellationToken cancellationToken)
  {
    var assetId = AssetId.Of(command.AssetId);
    var asset = await AssetRecordGuard.LoadAsync(context, assetId, track: true, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);

    if (await context.AssetAssignments.AnyAsync(a => a.AssetId == assetId && a.ExpectedReturnDate != null && a.ActualReturnDate == null, cancellationToken))
      return Result<DisposeAssetCommandResult>.Failure("This asset is out on loan. Record its return before disposing of it.");

    var methodId = DisposalMethodId.Of(command.DisposalMethodId);
    var method = await context.DisposalMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new DisposalMethodNotFoundException("The disposal method was not found. Choose it again.");

    var toStatusId = AssetStatusId.Of(command.ToStatusId);
    var toStatus = await context.AssetStatuses.AsNoTracking().FirstOrDefaultAsync(s => s.Id == toStatusId, cancellationToken)
      ?? throw new AssetStatusNotFoundException("The final status was not found. Choose it again.");

    Currency? currency = null;
    if (command.CurrencyCode is not null && command.DisposalValue is not null)
    {
      currency = Currency.Of(command.CurrencyCode);
      if (!await context.Currencies.AnyAsync(c => c.Id == currency && c.IsActive, cancellationToken))
        throw new CurrencyNotFoundException($"The currency {currency.Value} was not found or is switched off.");
    }

    var today = clock.Today;
    var now = clock.UtcNow;
    var actor = currentUser.Actor();
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId, cancellationToken);
    var schedules = await context.AssetDepreciationSchedules.Include(s => s.Entries).Where(s => s.AssetId == assetId).ToListAsync(cancellationToken);

    // months posted after the disposal date would be depreciation of an asset GDA no longer had
    if (AssetBookValue.PostedThrough(schedules) is { } posted && posted > command.DisposalDate)
      return Result<DisposeAssetCommandResult>.Failure($"Depreciation is posted up to {posted:d MMM yyyy}, after the disposal date. Reverse those months first, or choose a later disposal date.");

    // depreciate up to the disposal date first, so the book value at disposal is right
    var monthsPosted = 0;
    if (command.DisposalDate <= today)
    {
      foreach (var schedule in schedules.Where(s => s.IsActive))
      {
        var scheduleMethod = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
        if (scheduleMethod.Kind != DepreciationMethodKind.UnitsOfProduction && acquisition is not null)
          schedule.GenerateEntries(scheduleMethod, command.DisposalDate);
        monthsPosted += schedule.PostEndedEntries(command.DisposalDate, actor, now, today);
      }
    }

    var bookValue = acquisition is null ? (decimal?)null : AssetBookValue.NetBookValue(acquisition.AcquisitionCost, schedules, command.DisposalDate);

    var disposal = AssetDisposal.Create(
      AssetDisposalId.Of(Guid.NewGuid()), assetId, method, command.DisposalDate, command.DisposalValue, currency,
      acquisition?.CurrencyCode, acquisition?.AcquisitionDate, bookValue, command.BuyerInfo, command.Reason, actor, now, today);

    // the schedule stops on the disposal date; draft months after it go
    foreach (var schedule in schedules.Where(s => s.IsActive))
      schedule.Close(command.DisposalDate.AddDays(1));

    var fromStatus = await context.AssetStatuses.AsNoTracking().FirstAsync(s => s.Id == asset.StatusId, cancellationToken);
    asset.Dispose(fromStatus, toStatus);

    // stamped with the time it was recorded, so it sorts after everything else of that day
    var eventDate = command.DisposalDate == today ? now : command.DisposalDate.ToDateTime(TimeOnly.FromDateTime(now), DateTimeKind.Utc);
    var details = new Dictionary<string, string> { ["How"] = method.Name.Value };
    if (command.DisposalValue is { } proceeds) details["Proceeds"] = $"{proceeds.ToString("N2", CultureInfo.InvariantCulture)} {currency?.Value}";
    if (bookValue is { } nbv) details["Book value"] = nbv.ToString("N2", CultureInfo.InvariantCulture);
    await SystemEvents.AddAsync(context, assetId, EventPurpose.Disposal, eventDate, actor, command.Reason, JsonSerializer.SerializeToElement(details),
      fromStatus.Id, toStatus.Id, cancellationToken);

    await context.AssetDisposals.AddAsync(disposal, cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DisposeAssetCommandResult>.Success(new DisposeAssetCommandResult(disposal.Id.Value, disposal.NetBookValueAtDisposal, disposal.GainLoss, monthsPosted));
  }
}
