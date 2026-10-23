public sealed record EmploymentMethodId : ITypedId<EmploymentMethodId>
{
  public Guid Value { get; }

  private EmploymentMethodId(Guid value) => Value = value;

  public static EmploymentMethodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Employment method id cannot be empty.");

    return new EmploymentMethodId(value);
  }

  public static EmploymentMethodId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
