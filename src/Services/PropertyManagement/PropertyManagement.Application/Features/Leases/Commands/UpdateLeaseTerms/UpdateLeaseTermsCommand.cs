using FluentValidation;

public sealed record UpdateLeaseTermsCommandResult(bool IsSuccess);

/// Draft leases only — once in force, the terms are history and change through a renewal.
public sealed record UpdateLeaseTermsCommand(Guid Id, LeaseTermsInput Terms) : ICommand<Result<UpdateLeaseTermsCommandResult>>;

public class UpdateLeaseTermsCommandValidator : AbstractValidator<UpdateLeaseTermsCommand>
{
  public UpdateLeaseTermsCommandValidator()
  {
    RuleFor(x => x.Id).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new LeaseTermsInputValidator());
  }
}
