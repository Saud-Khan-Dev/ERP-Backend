public sealed record LocationId : ITypedId<LocationId>
{
  public Guid Value { get; }

  private LocationId(Guid value) => Value = value;

  public static LocationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Location id cannot be empty.");

    return new LocationId(value);
  }

  public static LocationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
