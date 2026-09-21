public sealed record LocationId
{
  public Guid Value { get; }

  private LocationId(Guid value) => Value = value;

  public static LocationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Location Id cannot be empty");

    return new LocationId(value);
  }
}
