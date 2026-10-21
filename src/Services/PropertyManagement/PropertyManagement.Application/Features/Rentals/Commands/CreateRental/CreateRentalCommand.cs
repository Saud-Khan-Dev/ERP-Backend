using FluentValidation;

/// Warnings: other agreements running on the property at the same time.
public sealed record CreateRentalCommandResult(Guid Id, string RentalNo, IReadOnlyList<string> Warnings);

/// RNT-00001 is generated; the rental starts ACTIVE, or as a DRAFT (terms still editable) with AsDraft.
public sealed record CreateRentalCommand(Guid PropertyId, Guid TenantOwnerId, RentalTermsInput Terms, bool AsDraft = false) : ICommand<Result<CreateRentalCommandResult>>;

public class CreateRentalCommandValidator : AbstractValidator<CreateRentalCommand>
{
  public CreateRentalCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.TenantOwnerId).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
  }
}
