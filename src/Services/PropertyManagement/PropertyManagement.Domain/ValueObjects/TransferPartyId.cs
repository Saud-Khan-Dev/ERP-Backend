public sealed record TransferPartyId
{
  public Guid Value { get; }

  private TransferPartyId(Guid value) => Value = value;

  public static TransferPartyId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Transfer party id cannot be empty.");

    return new TransferPartyId(value);
  }

  public static TransferPartyId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
