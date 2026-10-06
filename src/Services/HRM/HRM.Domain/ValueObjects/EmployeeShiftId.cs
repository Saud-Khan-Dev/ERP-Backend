public sealed record EmployeeShiftId : ITypedId<EmployeeShiftId>
{
  public Guid Value { get; }

  private EmployeeShiftId(Guid value) => Value = value;

  public static EmployeeShiftId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Shift assignment id cannot be empty.");

    return new EmployeeShiftId(value);
  }

  public static EmployeeShiftId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
