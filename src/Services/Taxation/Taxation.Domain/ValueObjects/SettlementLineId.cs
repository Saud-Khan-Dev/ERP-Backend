public sealed record SettlementLineId : ITypedId<SettlementLineId>
{
  public Guid Value { get; }

  private SettlementLineId(Guid value) => Value = value;

  public static SettlementLineId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Settlement line id cannot be empty.");

    return new SettlementLineId(value);
  }

  public static SettlementLineId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
