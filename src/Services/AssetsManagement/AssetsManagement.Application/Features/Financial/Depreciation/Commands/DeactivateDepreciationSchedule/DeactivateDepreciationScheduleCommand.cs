using FluentValidation;

public sealed record DeactivateDepreciationScheduleCommandResult(bool IsSuccess);

public sealed record DeactivateDepreciationScheduleCommand(Guid ScheduleId, DateOnly? EndDate = null) : ICommand<Result<DeactivateDepreciationScheduleCommandResult>>;

public class DeactivateDepreciationScheduleCommandValidator : AbstractValidator<DeactivateDepreciationScheduleCommand>
{
  public DeactivateDepreciationScheduleCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
  }
}
