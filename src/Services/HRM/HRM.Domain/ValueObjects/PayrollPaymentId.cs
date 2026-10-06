public sealed record PayrollPaymentId : ITypedId<PayrollPaymentId>
{
  public Guid Value { get; }

  private PayrollPaymentId(Guid value) => Value = value;

  public static PayrollPaymentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payment id cannot be empty.");

    return new PayrollPaymentId(value);
  }

  public static PayrollPaymentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
