using FluentValidation;

/// The editable facts of a property. The code is generated and the status changes through its own
/// endpoint, so neither is here.
public sealed record PropertyInput(
  string PropertyName,
  Guid TownId,
  Guid PropertyTypeId,
  Guid PropertyClassificationId,
  string? AddressLine = null,
  string? KhasraSurveyNo = null,
  string? Description = null,
  string? Remarks = null);

public sealed record MeasurementInput(
  Guid MeasurementUnitId,
  decimal TotalArea,
  decimal BuiltUpArea = 0,
  DateOnly? MeasuredOn = null,
  string? MeasurementSource = null,
  string? Remarks = null);

public class PropertyInputValidator : AbstractValidator<PropertyInput>
{
  public PropertyInputValidator()
  {
    RuleFor(x => x.PropertyName).NotEmpty().MaximumLength(Property.NameMaxLength);
    RuleFor(x => x.TownId).NotEmpty();
    RuleFor(x => x.PropertyTypeId).NotEmpty();
    RuleFor(x => x.PropertyClassificationId).NotEmpty();
    RuleFor(x => x.AddressLine).MaximumLength(300);
    RuleFor(x => x.KhasraSurveyNo).MaximumLength(100);
  }
}

public class MeasurementInputValidator : AbstractValidator<MeasurementInput>
{
  public MeasurementInputValidator()
  {
    RuleFor(x => x.MeasurementUnitId).NotEmpty();
    RuleFor(x => x.TotalArea).GreaterThan(0);
    RuleFor(x => x.BuiltUpArea).GreaterThanOrEqualTo(0);
    RuleFor(x => x.MeasurementSource).MaximumLength(150);
  }
}

/// Loads the four masters a property points at, checking each exists.
public static class PropertyMasters
{
  public sealed record Set(Town Town, PropertyType Type, PropertyClassification Classification);

  public static async Task<Set> LoadAsync(MasterLookup masters, PropertyInput input, CancellationToken cancellationToken) => new(
    await masters.GetAsync<Town>(input.TownId, cancellationToken),
    await masters.GetAsync<PropertyType>(input.PropertyTypeId, cancellationToken),
    await masters.GetAsync<PropertyClassification>(input.PropertyClassificationId, cancellationToken));
}
