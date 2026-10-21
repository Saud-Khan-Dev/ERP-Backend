using FluentValidation;

public sealed record UpdateRentalTermsCommandResult(bool IsSuccess);

/// A draft rental takes any change; once in force the terms are history (a renewal changes them) and only the agreement
/// reference, its date and the remarks can be corrected.
public sealed record UpdateRentalTermsCommand(Guid Id, RentalTermsInput Terms) : ICommand<Result<UpdateRentalTermsCommandResult>>;

public class UpdateRentalTermsCommandValidator : AbstractValidator<UpdateRentalTermsCommand>
{
  public UpdateRentalTermsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
  }
}
