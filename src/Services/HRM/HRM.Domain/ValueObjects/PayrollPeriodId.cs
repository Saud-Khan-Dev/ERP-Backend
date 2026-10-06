public sealed record PayrollPeriodId : ITypedId<PayrollPeriodId>
{
  public Guid Value { get; }

  private PayrollPeriodId(Guid value) => Value = value;

  public static PayrollPeriodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payroll period id cannot be empty.");

    return new PayrollPeriodId(value);
  }

  public static PayrollPeriodId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
