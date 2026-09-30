public sealed record EncumbranceId
{
  public Guid Value { get; }

  private EncumbranceId(Guid value) => Value = value;

  public static EncumbranceId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Encumbrance id cannot be empty.");

    return new EncumbranceId(value);
  }

  public static EncumbranceId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
