public sealed record PerformanceCompetencyId : ITypedId<PerformanceCompetencyId>
{
  public Guid Value { get; }

  private PerformanceCompetencyId(Guid value) => Value = value;

  public static PerformanceCompetencyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Competency id cannot be empty.");

    return new PerformanceCompetencyId(value);
  }

  public static PerformanceCompetencyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
