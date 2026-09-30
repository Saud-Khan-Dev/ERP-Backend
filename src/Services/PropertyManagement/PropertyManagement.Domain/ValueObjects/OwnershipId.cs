public sealed record OwnershipId
{
  public Guid Value { get; }

  private OwnershipId(Guid value) => Value = value;

  public static OwnershipId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Ownership id cannot be empty.");

    return new OwnershipId(value);
  }

  public static OwnershipId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
