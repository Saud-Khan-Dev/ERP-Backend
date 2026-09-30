using FluentValidation;

public sealed record SaveOwnerContactCommandResult(Guid ContactId);

/// ContactId null adds a contact; otherwise updates it. Marking one primary un-marks the others.
public sealed record SaveOwnerContactCommand(Guid OwnerId, Guid? ContactId, ContactInput Contact) : ICommand<Result<SaveOwnerContactCommandResult>>;

public sealed record DeactivateOwnerContactCommand(Guid OwnerId, Guid ContactId) : ICommand<Result<SaveOwnerContactCommandResult>>;

public class SaveOwnerContactCommandValidator : AbstractValidator<SaveOwnerContactCommand>
{
  public SaveOwnerContactCommandValidator()
  {
    RuleFor(x => x.OwnerId).NotEmpty();
    RuleFor(x => x.Contact).NotNull().SetValidator(new ContactInputValidator());
  }
}
