public sealed record TaxFileHeadValueId : ITypedId<TaxFileHeadValueId>
{
  public Guid Value { get; }

  private TaxFileHeadValueId(Guid value) => Value = value;

  public static TaxFileHeadValueId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Head value id cannot be empty.");

    return new TaxFileHeadValueId(value);
  }

  public static TaxFileHeadValueId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
