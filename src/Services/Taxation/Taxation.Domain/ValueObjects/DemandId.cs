public sealed record DemandId : ITypedId<DemandId>
{
  public Guid Value { get; }

  private DemandId(Guid value) => Value = value;

  public static DemandId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Demand id cannot be empty.");

    return new DemandId(value);
  }

  public static DemandId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
