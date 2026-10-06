public sealed record LoanTypeId : ITypedId<LoanTypeId>
{
  public Guid Value { get; }

  private LoanTypeId(Guid value) => Value = value;

  public static LoanTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Loan type id cannot be empty.");

    return new LoanTypeId(value);
  }

  public static LoanTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
