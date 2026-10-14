public sealed record DemandLineId : ITypedId<DemandLineId>
{
  public Guid Value { get; }

  private DemandLineId(Guid value) => Value = value;

  public static DemandLineId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Demand line id cannot be empty.");

    return new DemandLineId(value);
  }

  public static DemandLineId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
