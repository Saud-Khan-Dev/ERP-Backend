using FluentValidation;

/// Warnings: things recorded but worth checking (a boundary slope above 45°, BCR 2026). Never a refusal.
public sealed record CreatePropertyCommandResult(Guid Id, string PropertyCode, IReadOnlyList<string> Warnings);

/// Registers a property: PROP-00001 is generated, the opening status-history row is written and,
/// optionally, the first measurement, the first owners, the boundary survey and the custom-field values are
/// recorded — all in the same save, so either everything is registered or nothing is. Owners, when given, hold exactly
/// 100% between them (PropertyOwnership.RegisterFirstOwners): later owners come in only through a transfer.
public sealed record CreatePropertyCommand(
  PropertyInput Property,
  Guid PropertyStatusId,
  DateOnly? StatusEffectiveFrom = null,
  MeasurementInput? Measurement = null,
  IReadOnlyList<OwnershipInput>? Owners = null,
  BoundaryInput? Boundary = null,
  IReadOnlyList<AttributeValueInput>? Attributes = null) : ICommand<Result<CreatePropertyCommandResult>>;

public class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
  public CreatePropertyCommandValidator()
  {
    RuleFor(x => x.Property).NotNull().SetValidator(new PropertyInputValidator());
    RuleFor(x => x.PropertyStatusId).NotEmpty();
    RuleFor(x => x.StatusEffectiveFrom).LessThanOrEqualTo(_ => PropertyWriteGuard.Today())
      .When(x => x.StatusEffectiveFrom.HasValue).WithMessage("A status cannot start in the future.");
    RuleFor(x => x.Measurement!).SetValidator(new MeasurementInputValidator()).When(x => x.Measurement is not null);
    RuleForEach(x => x.Owners).NotNull().SetValidator(new OwnershipInputValidator());
    RuleFor(x => x.Boundary!).SetValidator(new BoundaryInputValidator()).When(x => x.Boundary is not null);
    RuleForEach(x => x.Attributes).NotNull().SetValidator(new AttributeValueInputValidator());
  }
}
