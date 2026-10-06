public sealed record PerformanceReviewId : ITypedId<PerformanceReviewId>
{
  public Guid Value { get; }

  private PerformanceReviewId(Guid value) => Value = value;

  public static PerformanceReviewId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Performance review id cannot be empty.");

    return new PerformanceReviewId(value);
  }

  public static PerformanceReviewId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
