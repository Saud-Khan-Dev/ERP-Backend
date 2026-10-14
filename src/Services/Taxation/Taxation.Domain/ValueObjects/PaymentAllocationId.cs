public sealed record PaymentAllocationId : ITypedId<PaymentAllocationId>
{
  public Guid Value { get; }

  private PaymentAllocationId(Guid value) => Value = value;

  public static PaymentAllocationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payment allocation id cannot be empty.");

    return new PaymentAllocationId(value);
  }

  public static PaymentAllocationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
