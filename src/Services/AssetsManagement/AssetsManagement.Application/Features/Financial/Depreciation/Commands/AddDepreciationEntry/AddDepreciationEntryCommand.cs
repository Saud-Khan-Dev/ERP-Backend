using FluentValidation;

public sealed record AddDepreciationEntryCommandResult(Guid Id);

/// A month entered by hand (units of production, where the amount comes from use). PeriodEnd is the period's LAST day.
public sealed record AddDepreciationEntryCommand(Guid ScheduleId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal DepreciationAmount)
  : ICommand<Result<AddDepreciationEntryCommandResult>>;

public class AddDepreciationEntryCommandValidator : AbstractValidator<AddDepreciationEntryCommand>
{
  public AddDepreciationEntryCommandValidator()
  {
    RuleFor(x => x.ScheduleId).NotEmpty();
    RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart).WithMessage("The period cannot end before it starts.");
    RuleFor(x => x.DepreciationAmount).GreaterThanOrEqualTo(0).WithMessage("The amount cannot be negative.");
  }
}
