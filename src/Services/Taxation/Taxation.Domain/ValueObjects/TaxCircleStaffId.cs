public sealed record TaxCircleStaffId : ITypedId<TaxCircleStaffId>
{
  public Guid Value { get; }

  private TaxCircleStaffId(Guid value) => Value = value;

  public static TaxCircleStaffId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Circle staff id cannot be empty.");

    return new TaxCircleStaffId(value);
  }

  public static TaxCircleStaffId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
