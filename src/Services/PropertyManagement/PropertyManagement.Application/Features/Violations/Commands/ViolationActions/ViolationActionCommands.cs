using FluentValidation;

public sealed record ViolationActionResult(Guid Id, ViolationStatus Status, FineStatus? FineStatus);

public sealed record IssueViolationNoticeCommand(Guid Id, string NoticeNo, DateOnly NoticeDate, DateOnly? NoticeDeadline) : ICommand<Result<ViolationActionResult>>;

/// s.28-A fine (up to Rs 1,000,000), imposed by the signed-in authorized officer.
public sealed record ImposeFineCommand(Guid Id, decimal FineAmount) : ICommand<Result<ViolationActionResult>>;

/// Paid / Waived / RecoveryAsArrears (s.28(2)). Collecting the money is the Tax / Finance Module's job.
public sealed record SetFineStatusCommand(Guid Id, FineStatus FineStatus) : ICommand<Result<ViolationActionResult>>;

public sealed record RectifyViolationCommand(Guid Id, string? Remarks = null) : ICommand<Result<ViolationActionResult>>;

public class IssueViolationNoticeCommandValidator : AbstractValidator<IssueViolationNoticeCommand>
{
  public IssueViolationNoticeCommandValidator() => RuleFor(x => x.NoticeNo).NotEmpty().MaximumLength(50);
}

public class ImposeFineCommandValidator : AbstractValidator<ImposeFineCommand>
{
  public ImposeFineCommandValidator() =>
    RuleFor(x => x.FineAmount).GreaterThan(0).LessThanOrEqualTo(AgreementViolation.MaxFine)
      .WithMessage($"The fine may extend to Rs {AgreementViolation.MaxFine:N0} (s.28-A).");
}

public class SetFineStatusCommandValidator : AbstractValidator<SetFineStatusCommand>
{
  public SetFineStatusCommandValidator() => RuleFor(x => x.FineStatus).IsInEnum();
}
