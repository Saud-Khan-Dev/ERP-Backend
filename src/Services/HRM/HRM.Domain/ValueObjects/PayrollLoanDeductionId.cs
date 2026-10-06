public sealed record PayrollLoanDeductionId : ITypedId<PayrollLoanDeductionId>
{
  public Guid Value { get; }

  private PayrollLoanDeductionId(Guid value) => Value = value;

  public static PayrollLoanDeductionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Loan deduction id cannot be empty.");

    return new PayrollLoanDeductionId(value);
  }

  public static PayrollLoanDeductionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
