public sealed record SalaryComponentId : ITypedId<SalaryComponentId>
{
  public Guid Value { get; }

  private SalaryComponentId(Guid value) => Value = value;

  public static SalaryComponentId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Salary component id cannot be empty.");

    return new SalaryComponentId(value);
  }

  public static SalaryComponentId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
