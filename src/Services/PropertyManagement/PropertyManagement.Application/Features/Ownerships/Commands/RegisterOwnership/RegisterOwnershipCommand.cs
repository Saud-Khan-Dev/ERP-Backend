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

public class RegisterOwnershipCommandValidator : AbstractValidator<RegisterOwnershipCommand>
{
  public RegisterOwnershipCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Ownership).NotNull();
    RuleFor(x => x.Ownership.OwnerId).NotEmpty();
    RuleFor(x => x.Ownership.TenureTypeId).NotEmpty();
    RuleFor(x => x.Ownership.OwnershipSharePct).GreaterThan(0).LessThanOrEqualTo(100);
    RuleFor(x => x.Ownership.ReferenceNo).MaximumLength(100);
  }
}
