using FluentValidation;

public sealed record AddDepreciationEntryCommandResult(Guid Id);

/// Manual entry, e.g. UNITS_OF_PRODUCTION where the amount comes from usage.
public sealed record AddDepreciationEntryCommand(Guid ScheduleId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal DepreciationAmount)
  : ICommand<Result<AddDepreciationEntryCommandResult>>;

public class AddDepreciationEntryCommandValidator : AbstractValidator<AddDepreciationEntryCommand>
{
  public AddDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.PeriodEnd).GreaterThan(x => x.PeriodStart);
    RuleFor(x => x.DepreciationAmount).GreaterThanOrEqualTo(0);
  }
}
