public sealed record TaxFileBasisId : ITypedId<TaxFileBasisId>
{
  public Guid Value { get; }

  private TaxFileBasisId(Guid value) => Value = value;

  public static TaxFileBasisId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax file basis id cannot be empty.");

    return new TaxFileBasisId(value);
  }

  public static TaxFileBasisId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
