public sealed record TaxOpeningId : ITypedId<TaxOpeningId>
{
  public Guid Value { get; }

  private TaxOpeningId(Guid value) => Value = value;

  public static TaxOpeningId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Opening tax position id cannot be empty.");

    return new TaxOpeningId(value);
  }

  public static TaxOpeningId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
