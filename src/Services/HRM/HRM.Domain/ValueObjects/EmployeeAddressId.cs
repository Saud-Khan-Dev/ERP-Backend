public sealed record EmployeeAddressId : ITypedId<EmployeeAddressId>
{
  public Guid Value { get; }

  private EmployeeAddressId(Guid value) => Value = value;

  public static EmployeeAddressId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Address id cannot be empty.");

    return new EmployeeAddressId(value);
  }

  public static EmployeeAddressId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
