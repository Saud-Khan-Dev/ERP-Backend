using Microsoft.EntityFrameworkCore;

public class DeactivateDepreciationScheduleHandler(IApplicationDbContext context, IClock clock)
  : ICommandHandler<DeactivateDepreciationScheduleCommand, Result<DeactivateDepreciationScheduleCommandResult>>
{
  public async Task<Result<DeactivateDepreciationScheduleCommandResult>> Handle(DeactivateDepreciationScheduleCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException("That depreciation schedule was not found.");

    await AssetRecordGuard.EnsureScheduleAssetOpenAsync(context, schedule.AssetId, cancellationToken);

    if (!schedule.IsActive)
      return Result<DeactivateDepreciationScheduleCommandResult>.Failure("This schedule is already closed.");

    schedule.Close((command.LastDay ?? clock.Today).AddDays(1));
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeactivateDepreciationScheduleCommandResult>.Success(new DeactivateDepreciationScheduleCommandResult(true));
  }
}
