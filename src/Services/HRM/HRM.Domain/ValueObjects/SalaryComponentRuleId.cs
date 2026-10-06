public sealed record SalaryComponentRuleId : ITypedId<SalaryComponentRuleId>
{
  public Guid Value { get; }

  private SalaryComponentRuleId(Guid value) => Value = value;

  public static SalaryComponentRuleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Salary rule id cannot be empty.");

    return new SalaryComponentRuleId(value);
  }

  public static SalaryComponentRuleId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
