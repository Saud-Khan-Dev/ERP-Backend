public sealed record LitigationPartyId
{
  public Guid Value { get; }

  private LitigationPartyId(Guid value) => Value = value;

  public static LitigationPartyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Litigation party id cannot be empty.");

    return new LitigationPartyId(value);
  }

  public static LitigationPartyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
