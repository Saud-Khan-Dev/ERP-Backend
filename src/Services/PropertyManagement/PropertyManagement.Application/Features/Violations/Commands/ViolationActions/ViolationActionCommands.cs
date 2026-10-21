using FluentValidation;

/// Warnings: a notice issued without a comply-by date (rule 5 cannot then apply).
public sealed record ViolationActionResult(Guid Id, ViolationStatus Status, FineStatus? FineStatus, IReadOnlyList<string> Warnings);

/// A violation carries one notice; a second is refused.
public sealed record IssueViolationNoticeCommand(Guid Id, string NoticeNo, DateOnly NoticeDate, DateOnly? NoticeDeadline) : ICommand<Result<ViolationActionResult>>;

/// s.28-A fine (up to Rs 1,000,000), imposed by the signed-in authorized officer.
public sealed record ImposeFineCommand(Guid Id, decimal FineAmount) : ICommand<Result<ViolationActionResult>>;

/// Paid (ReceiptNo and PaidOn required), Waived (Reason required; an approver's decision) or RecoveryAsArrears (s.28(2);
/// an approver's order). Paid and Waived are final. Collecting the money is the Tax / Finance Module's job.
public sealed record SetFineStatusCommand(Guid Id, FineStatus FineStatus, string? ReceiptNo = null, DateOnly? PaidOn = null, string? Reason = null)
  : ICommand<Result<ViolationActionResult>>;

public sealed record RectifyViolationCommand(Guid Id, string? Remarks = null) : ICommand<Result<ViolationActionResult>>;

public class IssueViolationNoticeCommandValidator : AbstractValidator<IssueViolationNoticeCommand>
{
  public IssueViolationNoticeCommandValidator()
  {
    RuleFor(x => x.NoticeNo).NotEmpty().MaximumLength(AgreementViolation.NoticeNoMaxLength);
    RuleFor(x => x.NoticeDate).LessThanOrEqualTo(_ => PropertyWriteGuard.Today()).WithMessage("A notice cannot be dated in the future.");
    RuleFor(x => x.NoticeDeadline).GreaterThanOrEqualTo(x => x.NoticeDate).When(x => x.NoticeDeadline.HasValue)
      .WithMessage("The comply-by date cannot be before the notice date.");
  }
}

public class RectifyViolationCommandValidator : AbstractValidator<RectifyViolationCommand>
{
  public RectifyViolationCommandValidator() => RuleFor(x => x.Remarks).MaximumLength(AgreementViolation.TextMaxLength);
}

public class ImposeFineCommandValidator : AbstractValidator<ImposeFineCommand>
{
  public ImposeFineCommandValidator() =>
    RuleFor(x => x.FineAmount).GreaterThan(0).LessThanOrEqualTo(AgreementViolation.MaxFine)
      .WithMessage($"The fine may extend to Rs {AgreementViolation.MaxFine:N0} (s.28-A).");
}

public class SetFineStatusCommandValidator : AbstractValidator<SetFineStatusCommand>
{
  public SetFineStatusCommandValidator()
  {
    RuleFor(x => x.FineStatus).IsInEnum();
    RuleFor(x => x.ReceiptNo).NotEmpty().When(x => x.FineStatus == FineStatus.Paid).WithMessage("Give the receipt no. the fine was paid against.")
      .MaximumLength(AgreementViolation.ReceiptNoMaxLength);
    RuleFor(x => x.PaidOn).NotNull().When(x => x.FineStatus == FineStatus.Paid).WithMessage("Give the date the fine was paid.");
    RuleFor(x => x.Reason).NotEmpty().When(x => x.FineStatus == FineStatus.Waived).WithMessage("Give the reason the fine is waived.")
      .MaximumLength(AgreementViolation.TextMaxLength);
  }
}
