/// One survey of total and built-up area. Kept as history: a new survey supersedes the current row
/// (is_current = false) instead of editing it.
///
/// "Current" means the latest dated survey: a survey measured before the one in force is stored as
/// history and does not supersede it; one of the same date or later does. An undated survey cannot be
/// compared, so it supersedes (and an undated current one is superseded).
public class PropertyMeasurement : Aggregate<PropertyMeasurementId>
{
  public const int SourceMaxLength = 150;
  public const int RemarksMaxLength = 4000;

  public PropertyId PropertyId { get; private set; } = default!;
  public MasterId MeasurementUnitId { get; private set; } = default!;
  /// Original / legal area, in the unit the user chose.
  public decimal TotalArea { get; private set; }
  public decimal BuiltUpArea { get; private set; }
  /// Same values in square feet, for reporting and comparison (schema guide, rule 7).
  public decimal TotalAreaBase { get; private set; }
  public decimal BuiltUpAreaBase { get; private set; }
  public DateOnly? MeasuredOn { get; private set; }
  /// Survey, allotment letter, site plan, GIS ...
  public string? MeasurementSource { get; private set; }
  public bool IsCurrent { get; private set; }
  public string? Remarks { get; private set; }

  /// Records a survey. currentMeasurements are the property's rows still marked current: they are superseded
  /// when this survey is the latest; otherwise this one is kept as history and a warning says so.
  public static PropertyMeasurement Record(
      PropertyMeasurementId id,
      Property property,
      MeasurementUnit unit,
      decimal totalArea,
      decimal builtUpArea,
      DateOnly? measuredOn,
      string? measurementSource,
      string? remarks,
      DateOnly today,
      IReadOnlyCollection<PropertyMeasurement> currentMeasurements,
      out IReadOnlyList<string> warnings)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(unit);
    ArgumentNullException.ThrowIfNull(currentMeasurements);
    property.EnsureActive();
    unit.EnsureActive();

    Guard.Positive(totalArea, "Total area");
    Guard.NotNegative(builtUpArea, "Built-up area");

    if (builtUpArea > totalArea)
      throw new DomainException("Built-up area cannot be more than the total area.");

    if (measuredOn > today)
      throw new DomainException("The measurement date cannot be in the future.");

    var measurement = new PropertyMeasurement
    {
      Id = id,
      PropertyId = property.Id,
      MeasurementUnitId = unit.Id,
      TotalArea = decimal.Round(totalArea, 4),
      BuiltUpArea = decimal.Round(builtUpArea, 4),
      TotalAreaBase = unit.ToBase(totalArea),
      BuiltUpAreaBase = unit.ToBase(builtUpArea),
      MeasuredOn = measuredOn,
      MeasurementSource = Guard.Text(measurementSource, SourceMaxLength, "Measurement source"),
      IsCurrent = true,
      Remarks = Guard.Text(remarks, RemarksMaxLength, "Remarks")
    };

    var current = currentMeasurements.Where(m => m.IsCurrent && m.PropertyId == property.Id).ToList();
    var newer = measuredOn is null ? null : current.Where(m => m.MeasuredOn > measuredOn).MaxBy(m => m.MeasuredOn);

    if (newer is not null)
    {
      measurement.IsCurrent = false;
      warnings = new[]
      {
        $"Measured on {measuredOn:yyyy-MM-dd}, before the current measurement of {newer.MeasuredOn:yyyy-MM-dd}: it is kept as history and the current measurement stays in force."
      };
      return measurement;
    }

    current.ForEach(m => m.Supersede());
    warnings = Array.Empty<string>();
    return measurement;
  }

  /// A newer survey replaced this one.
  public void Supersede() => IsCurrent = false;
}
