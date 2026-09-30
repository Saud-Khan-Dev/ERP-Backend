using FluentValidation;

public sealed record CreateRentalCommandResult(Guid Id, string RentalNo);

/// RNT-00001 is generated; the rental starts ACTIVE.
public sealed record CreateRentalCommand(Guid PropertyId, Guid TenantOwnerId, RentalTermsInput Terms) : ICommand<Result<CreateRentalCommandResult>>;

public class CreateRentalCommandValidator : AbstractValidator<CreateRentalCommand>
{
  public CreateRentalCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.TenantOwnerId).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
  }
}
