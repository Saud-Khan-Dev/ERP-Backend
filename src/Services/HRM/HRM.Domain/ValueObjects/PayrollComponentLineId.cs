public sealed record PayrollComponentLineId : ITypedId<PayrollComponentLineId>
{
  public Guid Value { get; }

  private PayrollComponentLineId(Guid value) => Value = value;

  public static PayrollComponentLineId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Pay-slip line id cannot be empty.");

    return new PayrollComponentLineId(value);
  }

  public static PayrollComponentLineId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
