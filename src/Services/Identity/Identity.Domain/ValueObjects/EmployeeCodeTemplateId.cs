public sealed record EmployeeCodeTemplateId
{
  public Guid Value { get; }

  private EmployeeCodeTemplateId(Guid value) => Value = value;

  public static EmployeeCodeTemplateId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Employee Code Template Id cannot be empty");

    return new EmployeeCodeTemplateId(value);
  }
}
