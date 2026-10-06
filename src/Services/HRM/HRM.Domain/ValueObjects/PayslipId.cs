public sealed record PayslipId : ITypedId<PayslipId>
{
  public Guid Value { get; }

  private PayslipId(Guid value) => Value = value;

  public static PayslipId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payslip id cannot be empty.");

    return new PayslipId(value);
  }

  public static PayslipId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
