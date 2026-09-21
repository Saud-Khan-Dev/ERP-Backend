using FluentValidation;

public sealed record CreateDepreciationScheduleCommandResult(Guid Id);

/// Starts depreciation for an asset. The depreciable base is snapshotted from the acquisition cost minus salvage.
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
    RuleFor(x => x.MethodId).NotEmpty();
    RuleFor(x => x.UsefulLifeMonths).GreaterThan(0);
    RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0);
    RuleFor(x => x.DecliningRate).ExclusiveBetween(0, 1).When(x => x.DecliningRate.HasValue);
  }
}
