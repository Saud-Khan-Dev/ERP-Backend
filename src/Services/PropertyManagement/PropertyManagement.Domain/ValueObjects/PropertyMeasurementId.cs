public sealed record PropertyMeasurementId
{
  public Guid Value { get; }

  private PropertyMeasurementId(Guid value) => Value = value;

  public static PropertyMeasurementId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Property measurement id cannot be empty.");

    return new PropertyMeasurementId(value);
  }

  public static PropertyMeasurementId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
