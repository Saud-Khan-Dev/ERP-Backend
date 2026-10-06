public sealed record EmployeeRequestTypeId : ITypedId<EmployeeRequestTypeId>
{
  public Guid Value { get; }

  private EmployeeRequestTypeId(Guid value) => Value = value;

  public static EmployeeRequestTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Request type id cannot be empty.");

    return new EmployeeRequestTypeId(value);
  }

  public static EmployeeRequestTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
