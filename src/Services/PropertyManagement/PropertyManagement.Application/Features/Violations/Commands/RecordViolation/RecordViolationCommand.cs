using FluentValidation;

public sealed record ViolationInput(
  Guid AgreementTypeId,
  Guid ViolatorOwnerId,
  DateOnly ViolationDate,
  string ViolationDescription,
  Guid? LeaseId = null,
  Guid? RentalId = null,
  Guid? TransferId = null,
  string? NoticeNo = null,
  DateOnly? NoticeDate = null,
  DateOnly? NoticeDeadline = null,
  string? Remarks = null);

/// OccurrenceNo tells which repetition this is (the agreement's violations dated on or before this one);
/// LedToCancellation is true when rule 5 cancelled the lease or rental (third violation within the period of
/// a notice served on an earlier one — Act s.28-A). Warnings: a notice without a comply-by date.
public sealed record RecordViolationCommandResult(Guid Id, short OccurrenceNo, bool LedToCancellation, IReadOnlyList<string> Warnings);

public sealed record RecordViolationCommand(Guid PropertyId, ViolationInput Violation) : ICommand<Result<RecordViolationCommandResult>>;

public class RecordViolationCommandValidator : AbstractValidator<RecordViolationCommand>
{
  public RecordViolationCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Violation).NotNull();
    RuleFor(x => x.Violation.AgreementTypeId).NotEmpty();
    RuleFor(x => x.Violation.ViolatorOwnerId).NotEmpty();
    RuleFor(x => x.Violation.ViolationDescription).NotEmpty().MaximumLength(AgreementViolation.TextMaxLength);
    RuleFor(x => x.Violation.ViolationDate).LessThanOrEqualTo(_ => PropertyWriteGuard.Today())
      .WithMessage("A violation cannot be dated in the future.");
    RuleFor(x => x.Violation.NoticeNo).MaximumLength(AgreementViolation.NoticeNoMaxLength);
    RuleFor(x => x.Violation.Remarks).MaximumLength(AgreementViolation.TextMaxLength);
    // rule 4: exactly one agreement
    RuleFor(x => x.Violation)
      .Must(v => new[] { v.LeaseId, v.RentalId, v.TransferId }.Count(id => id.HasValue) == 1)
      .WithMessage("Give exactly one of leaseId, rentalId or transferId.");
  }
}
