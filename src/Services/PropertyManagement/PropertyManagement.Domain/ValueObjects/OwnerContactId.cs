public sealed record OwnerContactId
{
  public Guid Value { get; }

  private OwnerContactId(Guid value) => Value = value;

  public static OwnerContactId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Owner contact id cannot be empty.");

    return new OwnerContactId(value);
  }

  public static OwnerContactId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
