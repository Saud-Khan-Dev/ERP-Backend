public sealed record SettlementBatchId : ITypedId<SettlementBatchId>
{
  public Guid Value { get; }

  private SettlementBatchId(Guid value) => Value = value;

  public static SettlementBatchId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Settlement batch id cannot be empty.");

    return new SettlementBatchId(value);
  }

  public static SettlementBatchId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
