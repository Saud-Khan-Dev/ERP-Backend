public sealed record TaxFileId : ITypedId<TaxFileId>
{
  public Guid Value { get; }

  private TaxFileId(Guid value) => Value = value;

  public static TaxFileId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax file id cannot be empty.");

    return new TaxFileId(value);
  }

  public static TaxFileId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
