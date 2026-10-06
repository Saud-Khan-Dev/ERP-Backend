public sealed record EmployeePayRecordId : ITypedId<EmployeePayRecordId>
{
  public Guid Value { get; }

  private EmployeePayRecordId(Guid value) => Value = value;

  public static EmployeePayRecordId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Pay record id cannot be empty.");

    return new EmployeePayRecordId(value);
  }

  public static EmployeePayRecordId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
