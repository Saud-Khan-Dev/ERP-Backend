using FluentValidation;

public sealed record EncumbranceInput(
  Guid EncumbranceTypeId,
  string HolderName,
  DateOnly StartDate,
  Guid? OwnershipId = null,
  Guid? HolderOwnerId = null,
  string? ReferenceNo = null,
  decimal? Amount = null,
  DateOnly? EndDate = null,
  string? Remarks = null);

public class EncumbranceInputValidator : AbstractValidator<EncumbranceInput>
{
  public EncumbranceInputValidator()
  {
    RuleFor(x => x.EncumbranceTypeId).NotEmpty();
    RuleFor(x => x.HolderName).NotEmpty().MaximumLength(200);
    RuleFor(x => x.ReferenceNo).MaximumLength(100);
    RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).When(x => x.Amount.HasValue);
  }
}

public sealed record RegisterEncumbranceCommandResult(Guid Id);

public sealed record RegisterEncumbranceCommand(Guid PropertyId, EncumbranceInput Encumbrance) : ICommand<Result<RegisterEncumbranceCommandResult>>;

public class RegisterEncumbranceCommandValidator : AbstractValidator<RegisterEncumbranceCommand>
{
  public RegisterEncumbranceCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.Encumbrance).NotNull().SetValidator(new EncumbranceInputValidator());
  }
}

/// The optional links of an encumbrance: which ownership is charged and whether the holder is a registered party.
public static class EncumbranceLinks
{
  public static async Task<(PropertyOwnership? Ownership, PropertyOwner? Holder)> LoadAsync(
      IApplicationDbContext context, EncumbranceInput input, CancellationToken cancellationToken)
  {
    var ownership = input.OwnershipId is { } ownershipId ? await context.LoadOwnershipAsync(ownershipId, cancellationToken) : null;
    var holder = input.HolderOwnerId is { } holderId ? await context.LoadOwnerAsync(holderId, cancellationToken) : null;
    return (ownership, holder);
  }
}
