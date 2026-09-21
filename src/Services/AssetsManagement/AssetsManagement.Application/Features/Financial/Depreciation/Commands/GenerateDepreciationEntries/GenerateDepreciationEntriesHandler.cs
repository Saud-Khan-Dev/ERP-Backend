using Microsoft.EntityFrameworkCore;

public class GenerateDepreciationEntriesHandler(IApplicationDbContext context)
  : ICommandHandler<GenerateDepreciationEntriesCommand, Result<GenerateDepreciationEntriesCommandResult>>
{
  public async Task<Result<GenerateDepreciationEntriesCommandResult>> Handle(GenerateDepreciationEntriesCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    var method = await context.DepreciationMethods.AsNoTracking().FirstAsync(m => m.Id == schedule.MethodId, cancellationToken);
    var acquisition = await context.AssetAcquisitions.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == schedule.AssetId, cancellationToken)
      ?? throw new AssetAcquisitionNotFoundException("The asset has no acquisition record.");

    var generated = schedule.GenerateEntries(method, command.Until ?? DateOnly.FromDateTime(DateTime.UtcNow), acquisition.AcquisitionCost);

    await context.SaveChangesAsync(cancellationToken);

    return Result<GenerateDepreciationEntriesCommandResult>.Success(
      new GenerateDepreciationEntriesCommandResult(generated.Select(e => e.ToDto()).ToList()));
  }
}
