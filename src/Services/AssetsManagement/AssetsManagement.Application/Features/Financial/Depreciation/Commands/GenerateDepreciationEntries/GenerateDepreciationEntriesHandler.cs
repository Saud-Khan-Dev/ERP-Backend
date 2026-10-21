using Microsoft.EntityFrameworkCore;

public class GenerateDepreciationEntriesHandler(IApplicationDbContext context, IClock clock)
  : ICommandHandler<GenerateDepreciationEntriesCommand, Result<GenerateDepreciationEntriesCommandResult>>
{
  public async Task<Result<GenerateDepreciationEntriesCommandResult>> Handle(GenerateDepreciationEntriesCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    var method = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
    var generated = schedule.GenerateEntries(method, command.Until ?? clock.Today);

    await context.SaveChangesAsync(cancellationToken);

    return Result<GenerateDepreciationEntriesCommandResult>.Success(
      new GenerateDepreciationEntriesCommandResult(generated.Select(e => e.ToDto()).ToList()));
  }
}
