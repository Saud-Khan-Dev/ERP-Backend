public sealed record TaxSlabId : ITypedId<TaxSlabId>
{
  public Guid Value { get; }

  private TaxSlabId(Guid value) => Value = value;

  public static TaxSlabId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax slab id cannot be empty.");

    return new TaxSlabId(value);
  }

  public static TaxSlabId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
