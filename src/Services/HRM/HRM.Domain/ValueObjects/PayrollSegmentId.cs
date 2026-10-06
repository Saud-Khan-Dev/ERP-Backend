public sealed record PayrollSegmentId : ITypedId<PayrollSegmentId>
{
  public Guid Value { get; }

  private PayrollSegmentId(Guid value) => Value = value;

  public static PayrollSegmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payroll segment id cannot be empty.");

    return new PayrollSegmentId(value);
  }

  public static PayrollSegmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
