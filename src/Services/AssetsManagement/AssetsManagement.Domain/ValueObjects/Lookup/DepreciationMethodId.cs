public sealed record DepreciationMethodId
{
  public Guid Value { get; }

  private DepreciationMethodId(Guid value) => Value = value;

  public static DepreciationMethodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Depreciation method Id cannot be empty");

    return new DepreciationMethodId(value);
  }
}
