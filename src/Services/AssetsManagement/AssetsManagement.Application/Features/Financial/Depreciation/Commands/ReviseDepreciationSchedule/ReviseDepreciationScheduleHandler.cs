using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class ReviseDepreciationScheduleHandler(IApplicationDbContext context, ICurrentUser currentUser, IClock clock)
  : ICommandHandler<ReviseDepreciationScheduleCommand, Result<ReviseDepreciationScheduleCommandResult>>
{
  public async Task<Result<ReviseDepreciationScheduleCommandResult>> Handle(ReviseDepreciationScheduleCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    var asset = await AssetRecordGuard.LoadAsync(context, schedule.AssetId, track: false, cancellationToken);
    await AssetRecordGuard.EnsureOpenAsync(context, asset, cancellationToken);
    var assetType = await context.AssetTypes.AsNoTracking().FirstAsync(t => t.Id == asset.AssetTypeId, cancellationToken);

    var methodId = command.MethodId is { } wanted ? DepreciationMethodId.Of(wanted) : schedule.MethodId;
    var method = await context.DepreciationMethods.AsNoTracking().FirstOrDefaultAsync(m => m.Id == methodId, cancellationToken)
      ?? throw new DepreciationMethodNotFoundException("The depreciation method was not found. Choose it again.");

    var oldMethod = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
    await using var transaction = await context.BeginTransactionAsync(cancellationToken);
    var revised = schedule.Revise(
      AssetDepreciationScheduleId.Of(Guid.NewGuid()), asset, assetType, method, command.RemainingLifeMonths, command.SalvageValue,
      command.DecliningRate ?? (method.Id == schedule.MethodId ? schedule.DecliningRate : null));

    // the old schedule is closed first: only one may be active (a unique index the database checks at once)
    await context.SaveChangesAsync(cancellationToken);
    await context.AssetDepreciationSchedules.AddAsync(revised, cancellationToken);
    var details = JsonSerializer.SerializeToElement(new Dictionary<string, string>
    {
      ["From"] = revised.StartDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
      ["Book value then"] = revised.OpeningBookValue.ToString("N2", CultureInfo.InvariantCulture),
      ["Remaining life"] = $"{command.RemainingLifeMonths} months",
      ["Value at the end"] = command.SalvageValue.ToString("N2", CultureInfo.InvariantCulture),
      ["Method"] = method.Id == oldMethod.Id ? method.Name.Value : $"{oldMethod.Name.Value} → {method.Name.Value}",
    });
    await SystemEvents.AddAsync(context, asset.Id, EventPurpose.DepreciationRevised, clock.UtcNow, currentUser.Actor(), command.Reason, details, cancellationToken: cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return Result<ReviseDepreciationScheduleCommandResult>.Success(new ReviseDepreciationScheduleCommandResult(revised.Id.Value));
  }
}
