using Microsoft.EntityFrameworkCore;

public class GenerateDepreciationEntriesHandler(IApplicationDbContext context, IClock clock, IAuditTrail audit)
  : ICommandHandler<GenerateDepreciationEntriesCommand, Result<GenerateDepreciationEntriesCommandResult>>
{
  public async Task<Result<GenerateDepreciationEntriesCommandResult>> Handle(GenerateDepreciationEntriesCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    var asset = await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    var method = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
    var until = command.Until ?? clock.Today;
    var generated = schedule.GenerateEntries(method, until);

    // the months are written in bulk (not one line each): one line says what was written
    if (generated.Count > 0)
      audit.Line(AuditActions.Added, AssetAuditKinds.DepreciationMonth, asset, schedule.Id.Value, $"{generated.Count} draft {(generated.Count == 1 ? "month" : "months")}",
        detail: AssetAudit.Join($"Up to {AssetAudit.Day(until)}", $"{AssetAudit.Month(generated[0])} - {AssetAudit.Month(generated[^1])}",
          $"Amount {AssetAudit.Amount(generated.Sum(e => e.DepreciationAmount))}"));

    await context.SaveChangesAsync(cancellationToken);

    return Result<GenerateDepreciationEntriesCommandResult>.Success(
      new GenerateDepreciationEntriesCommandResult(generated.Select(e => e.ToDto()).ToList()));
  }
}
