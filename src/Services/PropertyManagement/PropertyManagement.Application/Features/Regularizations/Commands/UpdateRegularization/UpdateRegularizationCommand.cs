using FluentValidation;

/// Moves the case along (status, dates, order). The area itself is fixed: a different area is a new case. A decided case
/// (Regularized / Rejected) is final: only EffectiveTo, the order reference and the remarks can still change.
public sealed record RegularizationCaseInput(
  RegularizationStatus Status,
  DateOnly? RegularizationDate = null,
  string? OrderReferenceNo = null,
  string? ApprovedBy = null,
  DateOnly? EffectiveFrom = null,
  DateOnly? EffectiveTo = null,
  string? Remarks = null,
  Guid? ApprovedByEmployeeId = null);

public sealed record UpdateRegularizationCommandResult(bool IsSuccess);

public sealed record UpdateRegularizationCommand(Guid Id, RegularizationCaseInput Case) : ICommand<Result<UpdateRegularizationCommandResult>>;

public class UpdateRegularizationCommandValidator : AbstractValidator<UpdateRegularizationCommand>
{
  public UpdateRegularizationCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Case).NotNull();
    RuleFor(x => x.Case.Status).IsInEnum();
    RuleFor(x => x.Case.OrderReferenceNo).MaximumLength(100);
    RuleFor(x => x.Case.ApprovedBy).MaximumLength(PropertyAreaRegularization.ApprovedByMaxLength);
    RuleFor(x => x.Case.Remarks).MaximumLength(4000);
  }
}
