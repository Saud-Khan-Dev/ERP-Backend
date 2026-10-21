using FluentValidation;

public sealed record DeactivateDepreciationScheduleCommandResult(bool IsSuccess);

/// Stops a running schedule. LastDay is the last day it still covers (default today); draft months after it go.
/// Months already posted stay - the schedule cannot stop before them.
public sealed record DeactivateDepreciationScheduleCommand(Guid ScheduleId, DateOnly? LastDay = null) : ICommand<Result<DeactivateDepreciationScheduleCommandResult>>;

public class DeactivateDepreciationScheduleCommandValidator : AbstractValidator<DeactivateDepreciationScheduleCommand>
{
  public DeactivateDepreciationScheduleCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
  }
}
