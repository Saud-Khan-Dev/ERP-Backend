public sealed record LitigationHoldId : ITypedId<LitigationHoldId>
{
  public Guid Value { get; }

  private LitigationHoldId(Guid value) => Value = value;

  public static LitigationHoldId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Litigation hold id cannot be empty.");

    return new LitigationHoldId(value);
  }

  public static LitigationHoldId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
