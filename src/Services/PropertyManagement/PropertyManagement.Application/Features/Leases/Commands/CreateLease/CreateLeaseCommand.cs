using FluentValidation;

public sealed record CreateLeaseCommandResult(Guid Id, string LeaseNo);

/// LSE-00001 is generated. AsDraft keeps the terms editable until the lease is activated.
public sealed record CreateLeaseCommand(Guid PropertyId, Guid LesseeOwnerId, LeaseTermsInput Terms, bool AsDraft = false) : ICommand<Result<CreateLeaseCommandResult>>;

public class CreateLeaseCommandValidator : AbstractValidator<CreateLeaseCommand>
{
  public CreateLeaseCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.LesseeOwnerId).NotEmpty();
    RuleFor(x => x.Terms).NotNull().SetValidator(new LeaseTermsInputValidator());
  }
}
