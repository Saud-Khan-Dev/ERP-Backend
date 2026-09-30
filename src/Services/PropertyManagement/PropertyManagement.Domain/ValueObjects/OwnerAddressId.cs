public sealed record OwnerAddressId
{
  public Guid Value { get; }

  private OwnerAddressId(Guid value) => Value = value;

  public static OwnerAddressId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Owner address id cannot be empty.");

    return new OwnerAddressId(value);
  }

  public static OwnerAddressId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
