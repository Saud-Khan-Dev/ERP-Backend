public sealed record TaxExemptionId : ITypedId<TaxExemptionId>
{
  public Guid Value { get; }

  private TaxExemptionId(Guid value) => Value = value;

  public static TaxExemptionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax exemption id cannot be empty.");

    return new TaxExemptionId(value);
  }

  public static TaxExemptionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
