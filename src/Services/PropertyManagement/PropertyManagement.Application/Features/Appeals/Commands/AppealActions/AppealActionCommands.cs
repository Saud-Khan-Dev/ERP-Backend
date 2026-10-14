using FluentValidation;

/// Effects: what the decision or withdrawal changed on the appealed record, in sentences for the clerk
/// ("Violation no. 3 of 2026-04-15 is set aside.", "Lease LSE-00004 is reinstated (ACTIVE again).").
public sealed record AppealActionResult(Guid Id, AppealStatus Status, DateOnly DecisionDueDate, IReadOnlyList<string> Warnings, IReadOnlyList<string> Effects);

public sealed record UpdateAppealCommand(Guid Id, AppealInput Appeal) : ICommand<Result<AppealActionResult>>;

public sealed record StartAppealHearingCommand(Guid Id) : ICommand<Result<AppealActionResult>>;

/// The decision is final (s.32).
public sealed record DecideAppealCommand(Guid Id, DateOnly DecisionDate, AppealOutcome DecisionOutcome, string? DecisionDetails = null) : ICommand<Result<AppealActionResult>>;

public sealed record WithdrawAppealCommand(Guid Id, string? Remarks = null) : ICommand<Result<AppealActionResult>>;

public class UpdateAppealCommandValidator : AbstractValidator<UpdateAppealCommand>
{
  public UpdateAppealCommandValidator() => RuleFor(x => x.Appeal).NotNull().SetValidator(new AppealInputValidator());
}

public class DecideAppealCommandValidator : AbstractValidator<DecideAppealCommand>
{
  public DecideAppealCommandValidator()
  {
    RuleFor(x => x.DecisionOutcome).IsInEnum();
    RuleFor(x => x.DecisionDetails).MaximumLength(4000);
  }
}

public class WithdrawAppealCommandValidator : AbstractValidator<WithdrawAppealCommand>
{
  public WithdrawAppealCommandValidator() => RuleFor(x => x.Remarks).MaximumLength(4000);
}
