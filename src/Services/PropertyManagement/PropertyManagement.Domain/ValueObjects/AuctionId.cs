public sealed record AuctionId
{
  public Guid Value { get; }

  private AuctionId(Guid value) => Value = value;

  public static AuctionId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Auction id cannot be empty.");

    return new AuctionId(value);
  }

  public static AuctionId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
