public sealed record PerformancePeriodId : ITypedId<PerformancePeriodId>
{
  public Guid Value { get; }

  private PerformancePeriodId(Guid value) => Value = value;

  public static PerformancePeriodId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Performance period id cannot be empty.");

    return new PerformancePeriodId(value);
  }

  public static PerformancePeriodId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
