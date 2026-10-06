public sealed record EmployeeId : ITypedId<EmployeeId>
{
  public Guid Value { get; }

  private EmployeeId(Guid value) => Value = value;

  public static EmployeeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Employee id cannot be empty.");

    return new EmployeeId(value);
  }

  public static EmployeeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
