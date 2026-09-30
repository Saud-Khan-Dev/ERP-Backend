using FluentValidation;

public sealed record ConfirmAllotmentOwnershipCommandResult(Guid OwnershipId);

/// GDA confirms the allottee as legal owner: a property_ownership row with acquired_via_allotment_id.
public sealed record ConfirmAllotmentOwnershipCommand(
  Guid AllotmentId,
  DateOnly EffectiveFrom,
  decimal OwnershipSharePct = 100,
  Guid? TenureTypeId = null,
  string? ReferenceNo = null,
  string? Remarks = null) : ICommand<Result<ConfirmAllotmentOwnershipCommandResult>>;

public class ConfirmAllotmentOwnershipCommandValidator : AbstractValidator<ConfirmAllotmentOwnershipCommand>
{
  public ConfirmAllotmentOwnershipCommandValidator()
  {
    RuleFor(x => x.AllotmentId).NotEmpty();
    RuleFor(x => x.OwnershipSharePct).GreaterThan(0).LessThanOrEqualTo(100);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
  }
}
