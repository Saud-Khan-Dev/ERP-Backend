public sealed record TaxCircleId : ITypedId<TaxCircleId>
{
  public Guid Value { get; }

  private TaxCircleId(Guid value) => Value = value;

  public static TaxCircleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax circle id cannot be empty.");

    return new TaxCircleId(value);
  }

  public static TaxCircleId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
