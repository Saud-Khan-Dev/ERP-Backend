public sealed record AuctionBidId
{
  public Guid Value { get; }

  private AuctionBidId(Guid value) => Value = value;

  public static AuctionBidId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Auction bid id cannot be empty.");

    return new AuctionBidId(value);
  }

  public static AuctionBidId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
