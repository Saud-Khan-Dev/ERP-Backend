public sealed record PaymentId : ITypedId<PaymentId>
{
  public Guid Value { get; }

  private PaymentId(Guid value) => Value = value;

  public static PaymentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payment id cannot be empty.");

    return new PaymentId(value);
  }

  public static PaymentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
