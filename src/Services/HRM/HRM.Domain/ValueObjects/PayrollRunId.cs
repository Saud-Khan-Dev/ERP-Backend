public sealed record PayrollRunId : ITypedId<PayrollRunId>
{
  public Guid Value { get; }

  private PayrollRunId(Guid value) => Value = value;

  public static PayrollRunId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payroll run id cannot be empty.");

    return new PayrollRunId(value);
  }

  public static PayrollRunId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
