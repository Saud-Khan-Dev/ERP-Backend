public sealed record PenaltyRuleId : ITypedId<PenaltyRuleId>
{
  public Guid Value { get; }

  private PenaltyRuleId(Guid value) => Value = value;

  public static PenaltyRuleId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Penalty rule id cannot be empty.");

    return new PenaltyRuleId(value);
  }

  public static PenaltyRuleId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
