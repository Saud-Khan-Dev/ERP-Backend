public sealed record TaxFilePayerId : ITypedId<TaxFilePayerId>
{
  public Guid Value { get; }

  private TaxFilePayerId(Guid value) => Value = value;

  public static TaxFilePayerId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payer id cannot be empty.");

    return new TaxFilePayerId(value);
  }

  public static TaxFilePayerId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
