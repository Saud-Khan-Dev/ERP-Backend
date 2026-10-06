public sealed record PayrollTransactionId : ITypedId<PayrollTransactionId>
{
  public Guid Value { get; }

  private PayrollTransactionId(Guid value) => Value = value;

  public static PayrollTransactionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Payroll transaction id cannot be empty.");

    return new PayrollTransactionId(value);
  }

  public static PayrollTransactionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
