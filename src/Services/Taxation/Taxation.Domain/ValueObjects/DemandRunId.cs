public sealed record DemandRunId : ITypedId<DemandRunId>
{
  public Guid Value { get; }

  private DemandRunId(Guid value) => Value = value;

  public static DemandRunId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Demand run id cannot be empty.");

    return new DemandRunId(value);
  }

  public static DemandRunId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
