public sealed record PenaltyId : ITypedId<PenaltyId>
{
  public Guid Value { get; }

  private PenaltyId(Guid value) => Value = value;

  public static PenaltyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Penalty id cannot be empty.");

    return new PenaltyId(value);
  }

  public static PenaltyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
