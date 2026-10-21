using FluentValidation;

/// Effects: what the act changed on a linked record ("Encroachment ENC-00012 is resolved by this decision.").
public sealed record LitigationActionResult(Guid Id, MasterRef? Status, DateOnly? NextHearingDate, IReadOnlyList<string>? Effects = null);

public sealed record UpdateLitigationCommand(Guid Id, LitigationDetailsInput Details) : ICommand<Result<LitigationActionResult>>;

public sealed record AddLitigationPartyCommand(Guid Id, LitigationPartyInput Party) : ICommand<Result<LitigationActionResult>>;

/// Logs a hearing that took place (not in the future); its next date becomes the case's next hearing date.
/// AttendedBy / AttendedByEmployeeId: the GDA officer who attended, picked from the staff list.
public sealed record RecordHearingCommand(
  Guid Id,
  DateOnly HearingDate,
  string? Proceedings = null,
  string? OrderPassed = null,
  DateOnly? NextHearingDate = null,
  string? AttendedBy = null,
  Guid? AttendedByEmployeeId = null) : ICommand<Result<LitigationActionResult>>;

/// Withdrawn, Settled, Closed ...
public sealed record ChangeLitigationStatusCommand(Guid Id, Guid LitigationStatusId) : ICommand<Result<LitigationActionResult>>;

/// An approver records the court's decision (not in the future): its wording and how it came out for GDA. With
/// ResolveRelatedEncroachment an unresolved encroachment the case was about is resolved (Litigated) on the decision date.
public sealed record DecideLitigationCommand(Guid Id, DateOnly DecisionDate, string DecisionOutcome, CaseResult? DecisionResult = null, bool ResolveRelatedEncroachment = false)
  : ICommand<Result<LitigationActionResult>>;

public class UpdateLitigationCommandValidator : AbstractValidator<UpdateLitigationCommand>
{
  public UpdateLitigationCommandValidator() => RuleFor(x => x.Details).NotNull().SetValidator(new LitigationDetailsInputValidator());
}

public class AddLitigationPartyCommandValidator : AbstractValidator<AddLitigationPartyCommand>
{
  public AddLitigationPartyCommandValidator() => RuleFor(x => x.Party).NotNull().SetValidator(new LitigationPartyInputValidator());
}

public class RecordHearingCommandValidator : AbstractValidator<RecordHearingCommand>
{
  public RecordHearingCommandValidator()
  {
    RuleFor(x => x.AttendedBy).MaximumLength(LitigationHearing.AttendedByMaxLength);
    RuleFor(x => x.Proceedings).MaximumLength(PropertyLitigation.TextMaxLength);
    RuleFor(x => x.OrderPassed).MaximumLength(PropertyLitigation.TextMaxLength);
    RuleFor(x => x.NextHearingDate).GreaterThan(x => x.HearingDate).When(x => x.NextHearingDate.HasValue)
      .WithMessage("The next hearing must be after this one.");
  }
}

public class DecideLitigationCommandValidator : AbstractValidator<DecideLitigationCommand>
{
  public DecideLitigationCommandValidator()
  {
    RuleFor(x => x.DecisionOutcome).NotEmpty().MaximumLength(4000);
    RuleFor(x => x.DecisionResult).NotNull().WithMessage("Say how the case came out for GDA: in favour, against, or partly in favour.").IsInEnum();
  }
}
