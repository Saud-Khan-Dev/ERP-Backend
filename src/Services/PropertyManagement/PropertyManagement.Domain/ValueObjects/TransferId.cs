public sealed record TransferId
{
  public Guid Value { get; }

  private TransferId(Guid value) => Value = value;

  public static TransferId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Transfer id cannot be empty.");

    return new TransferId(value);
  }

  public static TransferId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
