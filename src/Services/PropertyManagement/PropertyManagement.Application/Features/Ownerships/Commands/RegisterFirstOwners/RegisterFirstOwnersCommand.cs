using FluentValidation;

/// One owner's share as it stands in the register: who, how it is held, the share, since when, how it was acquired and
/// the deed / mutation it rests on. Sent for the first owners of a property (POST /properties/{id}/ownerships) and with a
/// new property (POST /properties).
public sealed record OwnershipInput(
  Guid OwnerId,
  Guid TenureTypeId,
  decimal OwnershipSharePct,
  DateOnly EffectiveFrom,
  Guid? AcquisitionTransferTypeId = null,
  string? ReferenceNo = null,
  string? Remarks = null);

public sealed record RegisterFirstOwnersCommandResult(IReadOnlyList<Guid> Ids);

/// Enters the owners of a property nobody holds now, all at once: together their shares make exactly 100%. Once a
/// property is held, owners change only through a transfer, so the chain of title is never broken.
public sealed record RegisterFirstOwnersCommand(Guid PropertyId, IReadOnlyList<OwnershipInput> Owners)
  : ICommand<Result<RegisterFirstOwnersCommandResult>>;

public class OwnershipInputValidator : AbstractValidator<OwnershipInput>
{
  public OwnershipInputValidator()
  {
    RuleFor(x => x.OwnerId).NotEmpty().WithMessage("Choose the owner.");
    RuleFor(x => x.TenureTypeId).NotEmpty().WithMessage("Choose how the property is held (the tenure).");
    RuleFor(x => x.OwnershipSharePct).GreaterThan(0).LessThanOrEqualTo(100)
      .WithMessage("A share is more than 0% and at most 100%.");
    RuleFor(x => x.EffectiveFrom).NotEmpty().WithMessage("The date the owner holds the share from (effectiveFrom) is required.");
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Remarks).MaximumLength(4000);
  }
}

public class RegisterFirstOwnersCommandValidator : AbstractValidator<RegisterFirstOwnersCommand>
{
  public RegisterFirstOwnersCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Owners).NotEmpty().WithMessage("Give at least one owner, with the share they hold.");
    RuleForEach(x => x.Owners).NotNull().SetValidator(new OwnershipInputValidator());
  }
}
