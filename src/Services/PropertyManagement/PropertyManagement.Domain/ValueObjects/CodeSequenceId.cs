public sealed record CodeSequenceId
{
  public Guid Value { get; }

  private CodeSequenceId(Guid value) => Value = value;

  public static CodeSequenceId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Code sequence id cannot be empty.");

    return new CodeSequenceId(value);
  }

  public static CodeSequenceId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
