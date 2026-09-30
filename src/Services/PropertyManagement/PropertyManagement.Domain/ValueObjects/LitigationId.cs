public sealed record LitigationId
{
  public Guid Value { get; }

  private LitigationId(Guid value) => Value = value;

  public static LitigationId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Litigation id cannot be empty.");

    return new LitigationId(value);
  }

  public static LitigationId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
