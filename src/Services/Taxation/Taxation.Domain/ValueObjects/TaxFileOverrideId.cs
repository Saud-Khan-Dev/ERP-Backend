public sealed record TaxFileOverrideId : ITypedId<TaxFileOverrideId>
{
  public Guid Value { get; }

  private TaxFileOverrideId(Guid value) => Value = value;

  public static TaxFileOverrideId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Override id cannot be empty.");

    return new TaxFileOverrideId(value);
  }

  public static TaxFileOverrideId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
