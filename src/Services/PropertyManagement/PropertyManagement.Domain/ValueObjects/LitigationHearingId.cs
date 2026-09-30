public sealed record LitigationHearingId
{
  public Guid Value { get; }

  private LitigationHearingId(Guid value) => Value = value;

  public static LitigationHearingId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Litigation hearing id cannot be empty.");

    return new LitigationHearingId(value);
  }

  public static LitigationHearingId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
