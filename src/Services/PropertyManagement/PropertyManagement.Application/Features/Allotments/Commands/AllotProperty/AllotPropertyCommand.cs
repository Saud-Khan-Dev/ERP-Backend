using FluentValidation;

public sealed record AllotmentDetailsInput(
  Guid AllotmentTypeId,
  DateOnly AllotmentDate,
  DateOnly? EffectiveDate = null,
  DateOnly? ExpiryDate = null,
  string? AllotmentLetterRef = null,
  string? Conditions = null,
  string? Remarks = null);

public class AllotmentDetailsInputValidator : AbstractValidator<AllotmentDetailsInput>
{
  public AllotmentDetailsInputValidator()
  {
    RuleFor(x => x.AllotmentTypeId).NotEmpty();
    RuleFor(x => x.AllotmentLetterRef).MaximumLength(100);
  }
}

/// Warnings: other agreements running on the plot at the same time (a lease, a rental, a contract).
public sealed record AllotPropertyCommandResult(Guid Id, string AllotmentNo, IReadOnlyList<string> Warnings);

/// An approver allots the plot (ALT-00001 is generated). The allotment starts ACTIVE and is in force from its effective
/// date (the allotment date when none is given); it is not ownership. Only GDA property nobody holds privately.
public sealed record AllotPropertyCommand(Guid PropertyId, Guid AllotteeOwnerId, AllotmentDetailsInput Allotment) : ICommand<Result<AllotPropertyCommandResult>>;

public class AllotPropertyCommandValidator : AbstractValidator<AllotPropertyCommand>
{
  public AllotPropertyCommandValidator()
  {
    RuleFor(x => x.PropertyId).NotEmpty();
    RuleFor(x => x.AllotteeOwnerId).NotEmpty();
    RuleFor(x => x.Allotment).NotNull().SetValidator(new AllotmentDetailsInputValidator());
  }
}
