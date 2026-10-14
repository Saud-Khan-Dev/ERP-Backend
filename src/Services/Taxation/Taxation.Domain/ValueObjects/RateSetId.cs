public sealed record RateSetId : ITypedId<RateSetId>
{
  public Guid Value { get; }

  private RateSetId(Guid value) => Value = value;

  public static RateSetId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Rate set id cannot be empty.");

    return new RateSetId(value);
  }

  public static RateSetId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
