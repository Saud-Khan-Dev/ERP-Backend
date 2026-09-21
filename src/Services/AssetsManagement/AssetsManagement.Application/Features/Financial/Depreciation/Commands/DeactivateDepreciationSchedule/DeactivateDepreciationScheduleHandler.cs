using Microsoft.EntityFrameworkCore;

public class DeactivateDepreciationScheduleHandler(IApplicationDbContext context)
  : ICommandHandler<DeactivateDepreciationScheduleCommand, Result<DeactivateDepreciationScheduleCommandResult>>
{
  public async Task<Result<DeactivateDepreciationScheduleCommandResult>> Handle(DeactivateDepreciationScheduleCommand command, CancellationToken cancellationToken)
  {
    var scheduleId = AssetDepreciationScheduleId.Of(command.ScheduleId);
    var schedule = await context.AssetDepreciationSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken)
      ?? throw new DepreciationScheduleNotFoundException($"Depreciation schedule {command.ScheduleId} was not found.");

    if (!schedule.IsActive)
      return Result<DeactivateDepreciationScheduleCommandResult>.Failure("This schedule is already inactive.");

    schedule.Deactivate(command.EndDate);
    await context.SaveChangesAsync(cancellationToken);

    return Result<DeactivateDepreciationScheduleCommandResult>.Success(new DeactivateDepreciationScheduleCommandResult(true));
  }
}
