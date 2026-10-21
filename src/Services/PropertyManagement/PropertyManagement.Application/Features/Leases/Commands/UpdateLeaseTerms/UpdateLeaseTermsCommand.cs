using FluentValidation;

public sealed record UpdateLeaseTermsCommandResult(bool IsSuccess);

/// A draft lease takes any change; once in force the terms are history (a renewal changes them) and only the agreement
/// reference, its date and the remarks can be corrected.
public sealed record UpdateLeaseTermsCommand(Guid Id, LeaseTermsInput Terms) : ICommand<Result<UpdateLeaseTermsCommandResult>>;

public class UpdateLeaseTermsCommandValidator : AbstractValidator<UpdateLeaseTermsCommand>
{
  public UpdateLeaseTermsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new LeaseTermsInputValidator());
  }
}
