public sealed record PenaltyRunId : ITypedId<PenaltyRunId>
{
  public Guid Value { get; }

  private PenaltyRunId(Guid value) => Value = value;

  public static PenaltyRunId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Penalty run id cannot be empty.");

    return new PenaltyRunId(value);
  }

  public static PenaltyRunId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
