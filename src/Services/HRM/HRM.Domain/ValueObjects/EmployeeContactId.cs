public sealed record EmployeeContactId : ITypedId<EmployeeContactId>
{
  public Guid Value { get; }

  private EmployeeContactId(Guid value) => Value = value;

  public static EmployeeContactId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Contact id cannot be empty.");

    return new EmployeeContactId(value);
  }

  public static EmployeeContactId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
