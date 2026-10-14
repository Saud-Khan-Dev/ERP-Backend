public sealed record TaxHeadId : ITypedId<TaxHeadId>
{
  public Guid Value { get; }

  private TaxHeadId(Guid value) => Value = value;

  public static TaxHeadId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax head id cannot be empty.");

    return new TaxHeadId(value);
  }

  public static TaxHeadId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
