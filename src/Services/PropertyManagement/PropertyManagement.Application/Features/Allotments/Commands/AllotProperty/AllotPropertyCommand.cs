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

public sealed record AllotPropertyCommandResult(Guid Id, string AllotmentNo);

/// Allots the plot (ALT-00001 is generated). The allotment starts ACTIVE; it is not ownership.
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
