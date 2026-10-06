public sealed record PerformanceGoalId : ITypedId<PerformanceGoalId>
{
  public Guid Value { get; }

  private PerformanceGoalId(Guid value) => Value = value;

  public static PerformanceGoalId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Goal id cannot be empty.");

    return new PerformanceGoalId(value);
  }

  public static PerformanceGoalId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
