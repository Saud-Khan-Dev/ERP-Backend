public sealed record EmployeeRequestId : ITypedId<EmployeeRequestId>
{
  public Guid Value { get; }

  private EmployeeRequestId(Guid value) => Value = value;

  public static EmployeeRequestId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Request id cannot be empty.");

    return new EmployeeRequestId(value);
  }

  public static EmployeeRequestId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
