public sealed record MasterId
{
  public Guid Value { get; }

  private MasterId(Guid value) => Value = value;

  public static MasterId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Master data id cannot be empty.");

    return new MasterId(value);
  }

  public static MasterId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
