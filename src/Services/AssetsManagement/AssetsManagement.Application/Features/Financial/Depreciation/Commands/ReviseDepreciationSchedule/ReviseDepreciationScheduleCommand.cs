using FluentValidation;

public sealed record ReviseDepreciationScheduleCommandResult(Guid ScheduleId);

/// A change of estimate (a longer or shorter life, another value at the end, another method): the running schedule
/// stops after its last posted month, and a new one spreads the book value left over the remaining life. Nothing
/// already posted changes.
public sealed record ReviseDepreciationScheduleCommand(
  Guid ScheduleId,
  int RemainingLifeMonths,
  decimal SalvageValue,
  Guid? MethodId = null,
  decimal? DecliningRate = null,
  string? Reason = null) : ICommand<Result<ReviseDepreciationScheduleCommandResult>>;

public class ReviseDepreciationScheduleCommandValidator : AbstractValidator<ReviseDepreciationScheduleCommand>
{
  public ReviseDepreciationScheduleCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.RemainingLifeMonths).InclusiveBetween(1, 1200).WithMessage("The remaining life must be between 1 month and 100 years.");
    RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).WithMessage("The value at the end cannot be negative.");
    RuleFor(x => x.DecliningRate).ExclusiveBetween(0, 1).When(x => x.DecliningRate.HasValue).WithMessage("The yearly rate must be between 0 % and 100 %.");
    RuleFor(x => x.Reason).MaximumLength(500);
  }
}
