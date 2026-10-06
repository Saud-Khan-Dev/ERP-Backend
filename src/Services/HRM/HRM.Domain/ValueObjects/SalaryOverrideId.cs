public sealed record SalaryOverrideId : ITypedId<SalaryOverrideId>
{
  public Guid Value { get; }

  private SalaryOverrideId(Guid value) => Value = value;

  public static SalaryOverrideId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Salary override id cannot be empty.");

    return new SalaryOverrideId(value);
  }

  public static SalaryOverrideId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
