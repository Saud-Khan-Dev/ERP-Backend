public sealed record EmploymentTypeId : ITypedId<EmploymentTypeId>
{
  public Guid Value { get; }

  private EmploymentTypeId(Guid value) => Value = value;

  public static EmploymentTypeId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Employment type id cannot be empty.");

    return new EmploymentTypeId(value);
  }

  public static EmploymentTypeId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
