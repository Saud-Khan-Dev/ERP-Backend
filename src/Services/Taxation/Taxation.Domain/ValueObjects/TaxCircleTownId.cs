public sealed record TaxCircleTownId : ITypedId<TaxCircleTownId>
{
  public Guid Value { get; }

  private TaxCircleTownId(Guid value) => Value = value;

  public static TaxCircleTownId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Circle town id cannot be empty.");

    return new TaxCircleTownId(value);
  }

  public static TaxCircleTownId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
