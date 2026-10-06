public sealed record PerformanceKpiId : ITypedId<PerformanceKpiId>
{
  public Guid Value { get; }

  private PerformanceKpiId(Guid value) => Value = value;

  public static PerformanceKpiId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("KPI id cannot be empty.");

    return new PerformanceKpiId(value);
  }

  public static PerformanceKpiId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
