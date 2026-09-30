using FluentValidation;

public sealed record UpdateRentalTermsCommandResult(bool IsSuccess);

public sealed record UpdateRentalTermsCommand(Guid Id, RentalTermsInput Terms) : ICommand<Result<UpdateRentalTermsCommandResult>>;

public class UpdateRentalTermsCommandValidator : AbstractValidator<UpdateRentalTermsCommand>
{
  public UpdateRentalTermsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new RentalTermsInputValidator());
  }
}
