public sealed record EmployeeTaskId : ITypedId<EmployeeTaskId>
{
  public Guid Value { get; }

  private EmployeeTaskId(Guid value) => Value = value;

  public static EmployeeTaskId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Task id cannot be empty.");

    return new EmployeeTaskId(value);
  }

  public static EmployeeTaskId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
