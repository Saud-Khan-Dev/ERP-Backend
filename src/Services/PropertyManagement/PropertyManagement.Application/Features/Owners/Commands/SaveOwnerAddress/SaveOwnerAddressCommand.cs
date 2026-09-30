using FluentValidation;

public sealed record SaveOwnerAddressCommandResult(Guid AddressId);

/// AddressId null adds an address; otherwise updates it. Marking one primary un-marks the others.
public sealed record SaveOwnerAddressCommand(Guid OwnerId, Guid? AddressId, AddressInput Address) : ICommand<Result<SaveOwnerAddressCommandResult>>;

public sealed record DeactivateOwnerAddressCommand(Guid OwnerId, Guid AddressId) : ICommand<Result<SaveOwnerAddressCommandResult>>;

public class SaveOwnerAddressCommandValidator : AbstractValidator<SaveOwnerAddressCommand>
{
  public SaveOwnerAddressCommandValidator()
  {
    RuleFor(x => x.OwnerId).NotEmpty();
    RuleFor(x => x.Address).NotNull().SetValidator(new AddressInputValidator());
  }
}
