public sealed record HolidayId : ITypedId<HolidayId>
{
  public Guid Value { get; }

  private HolidayId(Guid value) => Value = value;

  public static HolidayId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Holiday id cannot be empty.");

    return new HolidayId(value);
  }

  public static HolidayId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
