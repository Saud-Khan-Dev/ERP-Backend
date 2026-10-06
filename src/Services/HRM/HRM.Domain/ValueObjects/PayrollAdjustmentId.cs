public sealed record PayrollAdjustmentId : ITypedId<PayrollAdjustmentId>
{
  public Guid Value { get; }

  private PayrollAdjustmentId(Guid value) => Value = value;

  public static PayrollAdjustmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payroll adjustment id cannot be empty.");

    return new PayrollAdjustmentId(value);
  }

  public static PayrollAdjustmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
