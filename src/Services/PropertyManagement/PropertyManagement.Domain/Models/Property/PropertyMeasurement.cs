/// One survey of total and built-up area. Kept as history: a new survey supersedes the current row
/// (is_current = false) instead of editing it.
public class PropertyMeasurement : Aggregate<PropertyMeasurementId>
{
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

  public static PropertyMeasurement Record(
      PropertyMeasurementId id,
      Property property,
      MeasurementUnit unit,
      decimal totalArea,
      decimal builtUpArea,
      DateOnly? measuredOn,
      string? measurementSource,
      string? remarks)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(unit);
    property.EnsureActive();
    unit.EnsureActive();

    Guard.Positive(totalArea, "Total area");
    Guard.NotNegative(builtUpArea, "Built-up area");

    return new PropertyMeasurement
    {
      Id = id,
      PropertyId = property.Id,
      MeasurementUnitId = unit.Id,
      TotalArea = decimal.Round(totalArea, 4),
      BuiltUpArea = decimal.Round(builtUpArea, 4),
      TotalAreaBase = unit.ToBase(totalArea),
      BuiltUpAreaBase = unit.ToBase(builtUpArea),
      MeasuredOn = measuredOn,
      MeasurementSource = Guard.Text(measurementSource, 150, "Measurement source"),
      IsCurrent = true,
      Remarks = Guard.Text(remarks, 4000, "Remarks")
    };
  }

  /// A newer survey replaced this one.
  public void Supersede() => IsCurrent = false;
}
