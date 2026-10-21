public sealed record LoanRepaymentId : ITypedId<LoanRepaymentId>
{
  public Guid Value { get; }

  private LoanRepaymentId(Guid value) => Value = value;

  public static LoanRepaymentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Loan repayment id cannot be empty.");

    return new LoanRepaymentId(value);
  }

  public static LoanRepaymentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
