using FluentValidation;

public sealed record LitigationActionResult(Guid Id, MasterRef? Status, DateOnly? NextHearingDate);

public sealed record UpdateLitigationCommand(Guid Id, LitigationDetailsInput Details) : ICommand<Result<LitigationActionResult>>;

public sealed record AddLitigationPartyCommand(Guid Id, LitigationPartyInput Party) : ICommand<Result<LitigationActionResult>>;

/// Logs a hearing; its next date becomes the case's next hearing date.
public sealed record RecordHearingCommand(
  Guid Id,
  DateOnly HearingDate,
  string? Proceedings = null,
  string? OrderPassed = null,
  DateOnly? NextHearingDate = null,
  string? AttendedBy = null) : ICommand<Result<LitigationActionResult>>;

/// Withdrawn, Settled, Closed ...
public sealed record ChangeLitigationStatusCommand(Guid Id, Guid LitigationStatusId) : ICommand<Result<LitigationActionResult>>;

public sealed record DecideLitigationCommand(Guid Id, DateOnly DecisionDate, string DecisionOutcome) : ICommand<Result<LitigationActionResult>>;

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
  public DecideLitigationCommandValidator() => RuleFor(x => x.DecisionOutcome).NotEmpty().MaximumLength(4000);
}
