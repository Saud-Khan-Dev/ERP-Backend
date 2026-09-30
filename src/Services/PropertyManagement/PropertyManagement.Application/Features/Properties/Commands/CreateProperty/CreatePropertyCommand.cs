using FluentValidation;

public sealed record CreatePropertyCommandResult(Guid Id, string PropertyCode);

/// Registers a property: PROP-00001 is generated, the opening status-history row is written and,
/// optionally, the first measurement is recorded in the same transaction.
public sealed record CreatePropertyCommand(
  PropertyInput Property,
  Guid PropertyStatusId,
  DateOnly? StatusEffectiveFrom = null,
  MeasurementInput? Measurement = null) : ICommand<Result<CreatePropertyCommandResult>>;

public class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
  public CreatePropertyCommandValidator()
  {
    RuleFor(x => x.Property).NotNull().SetValidator(new PropertyInputValidator());
    RuleFor(x => x.PropertyStatusId).NotEmpty();
    RuleFor(x => x.Measurement!).SetValidator(new MeasurementInputValidator()).When(x => x.Measurement is not null);
  }
}
