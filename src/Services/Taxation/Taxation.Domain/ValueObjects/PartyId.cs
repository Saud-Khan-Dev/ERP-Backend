public sealed record PartyId : ITypedId<PartyId>
{
  public Guid Value { get; }

  private PartyId(Guid value) => Value = value;

  public static PartyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Party id cannot be empty.");

    return new PartyId(value);
  }

  public static PartyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
