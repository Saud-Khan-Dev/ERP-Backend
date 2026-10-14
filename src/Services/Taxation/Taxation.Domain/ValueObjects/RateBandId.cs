public sealed record RateBandId : ITypedId<RateBandId>
{
  public Guid Value { get; }

  private RateBandId(Guid value) => Value = value;

  public static RateBandId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Rate band id cannot be empty.");

    return new RateBandId(value);
  }

  public static RateBandId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
