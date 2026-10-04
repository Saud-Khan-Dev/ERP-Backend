using FluentValidation;

/// Records an owner's share directly — the property's first owners, or a correction from the paper
/// record. Changes between owners go through a transfer instead, so they keep their history.
public sealed record OwnershipInput(
  Guid OwnerId,
  Guid TenureTypeId,
  decimal OwnershipSharePct,
  DateOnly EffectiveFrom,
  Guid? AcquisitionTransferTypeId = null,
  string? ReferenceNo = null,
  string? Remarks = null);

public sealed record RegisterOwnershipCommandResult(Guid Id);

public sealed record RegisterOwnershipCommand(Guid PropertyId, OwnershipInput Ownership) : ICommand<Result<RegisterOwnershipCommandResult>>;

public class OwnershipInputValidator : AbstractValidator<OwnershipInput>
{
  public OwnershipInputValidator()
  {
    RuleFor(x => x.OwnerId).NotEmpty();
    RuleFor(x => x.TenureTypeId).NotEmpty();
    RuleFor(x => x.OwnershipSharePct).GreaterThan(0).LessThanOrEqualTo(100);
    RuleFor(x => x.EffectiveFrom).NotEmpty().WithMessage("The date the owner holds the share from (effectiveFrom) is required.");
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
  }
}

public class RegisterOwnershipCommandValidator : AbstractValidator<RegisterOwnershipCommand>
{
  public RegisterOwnershipCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Ownership).NotNull().SetValidator(new OwnershipInputValidator());
  }
}
