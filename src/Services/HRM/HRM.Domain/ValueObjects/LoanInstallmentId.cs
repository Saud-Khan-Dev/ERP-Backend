public sealed record LoanInstallmentId : ITypedId<LoanInstallmentId>
{
  public Guid Value { get; }

  private LoanInstallmentId(Guid value) => Value = value;

  public static LoanInstallmentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Installment id cannot be empty.");

    return new LoanInstallmentId(value);
  }

  public static LoanInstallmentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
