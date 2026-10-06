public sealed record TaxYearId : ITypedId<TaxYearId>
{
  public Guid Value { get; }

  private TaxYearId(Guid value) => Value = value;

  public static TaxYearId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Tax year id cannot be empty.");

    return new TaxYearId(value);
  }

  public static TaxYearId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
