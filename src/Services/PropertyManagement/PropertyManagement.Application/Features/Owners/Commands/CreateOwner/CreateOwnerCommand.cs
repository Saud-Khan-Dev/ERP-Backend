using FluentValidation;

public sealed record CreateOwnerCommandResult(Guid Id, string OwnerCode);

/// Registers a person or organization (OWN-00001 is generated), optionally with contacts and addresses.
public sealed record CreateOwnerCommand(
  OwnerInput Owner,
  IReadOnlyList<ContactInput>? Contacts = null,
  IReadOnlyList<AddressInput>? Addresses = null) : ICommand<Result<CreateOwnerCommandResult>>;

public class CreateOwnerCommandValidator : AbstractValidator<CreateOwnerCommand>
{
  public CreateOwnerCommandValidator()
  {
    RuleFor(x => x.Owner).NotNull().SetValidator(new OwnerInputValidator());
    RuleForEach(x => x.Contacts).SetValidator(new ContactInputValidator());
    RuleForEach(x => x.Addresses).SetValidator(new AddressInputValidator());
  }
}
