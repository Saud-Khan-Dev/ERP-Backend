public sealed record EmployeeLoanId : ITypedId<EmployeeLoanId>
{
  public Guid Value { get; }

  private EmployeeLoanId(Guid value) => Value = value;

  public static EmployeeLoanId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Loan id cannot be empty.");

    return new EmployeeLoanId(value);
  }

  public static EmployeeLoanId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
