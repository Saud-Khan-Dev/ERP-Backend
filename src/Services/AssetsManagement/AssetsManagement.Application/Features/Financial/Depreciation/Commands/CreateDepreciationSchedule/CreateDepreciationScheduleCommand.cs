using FluentValidation;

public sealed record CreateDepreciationScheduleCommandResult(Guid Id);

/// Starts depreciating an asset that has no running schedule. It starts from the asset's book value now (its cost, less
/// whatever earlier schedules posted), after the last posted month.
public sealed record CreateDepreciationScheduleCommand(
  Guid AssetId,
  Guid MethodId,
  int UsefulLifeMonths,
  decimal SalvageValue,
  DateOnly StartDate,
  decimal? DecliningRate = null) : ICommand<Result<CreateDepreciationScheduleCommandResult>>;

public class CreateDepreciationScheduleCommandValidator : AbstractValidator<CreateDepreciationScheduleCommand>
{
  public CreateDepreciationScheduleCommandValidator()
  {
    RuleFor(x => x.AssetId).NotEmpty();
    RuleFor(x => x.MethodId).NotEmpty().WithMessage("Choose how it depreciates.");
    RuleFor(x => x.UsefulLifeMonths).InclusiveBetween(1, 1200).WithMessage("The useful life must be between 1 month and 100 years.");
    RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).WithMessage("The value at the end cannot be negative.");
    RuleFor(x => x.DecliningRate).ExclusiveBetween(0, 1).When(x => x.DecliningRate.HasValue).WithMessage("The yearly rate must be between 0 % and 100 %.");
  }
}
