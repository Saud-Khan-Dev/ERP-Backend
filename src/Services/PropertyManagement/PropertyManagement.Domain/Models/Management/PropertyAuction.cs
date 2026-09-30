/// One auction of the property (auction committee — Act s.9), with the bids received.
///
/// Recording every bid is optional (open question with GDA): an auction can be awarded to one of its
/// recorded bids or, when only the winner is kept, straight to the successful bidder. An award may lead to
/// a transfer, allotment or lease, which are recorded separately.
public class PropertyAuction : Aggregate<AuctionId>
{
  private readonly List<AuctionBid> _bids = new();

  public PropertyId PropertyId { get; private set; } = default!;
  /// AUC-00001, issued by the AUCTION code sequence.
  public BusinessCode AuctionNo { get; private set; } = default!;
  public MasterId AuctionTypeId { get; private set; } = default!;
  public MasterId AuctionStatusId { get; private set; } = default!;
  public DateOnly? AnnouncementDate { get; private set; }
  public DateOnly? AuctionDate { get; private set; }
  public decimal? BaseReservePrice { get; private set; }
  public decimal? WinningBidAmount { get; private set; }
  public OwnerId? SuccessfulBidderOwnerId { get; private set; }
  public DateOnly? AwardDate { get; private set; }
  public string? AwardReferenceNo { get; private set; }
  /// Auction Committee minutes reference (Act s.9).
  public string? AuctionCommitteeRef { get; private set; }
  public string? Venue { get; private set; }
  public string? ReferenceNo { get; private set; }
  public string? Remarks { get; private set; }

  public IReadOnlyList<AuctionBid> Bids => _bids.AsReadOnly();

  public sealed record Details(
      AuctionType AuctionType,
      DateOnly? AnnouncementDate,
      DateOnly? AuctionDate,
      decimal? BaseReservePrice,
      string? AuctionCommitteeRef,
      string? Venue,
      string? ReferenceNo,
      string? Remarks);

  public static PropertyAuction Plan(AuctionId id, Property property, BusinessCode auctionNo, AuctionStatus plannedStatus, Details details)
  {
    ArgumentNullException.ThrowIfNull(property);
    ArgumentNullException.ThrowIfNull(auctionNo);
    ArgumentNullException.ThrowIfNull(plannedStatus);
    property.EnsureActive();
    plannedStatus.EnsureIs(SystemMasterCodes.Planned);

    var auction = new PropertyAuction { Id = id, PropertyId = property.Id, AuctionNo = auctionNo, AuctionStatusId = plannedStatus.Id };
    auction.Apply(details);
    return auction;
  }

  public void Update(AuctionStatus currentStatus, Details details)
  {
    EnsureOpen(currentStatus);
    Apply(details);
  }

  /// Announced, Conducted, Successful, Unsuccessful ... Awarding and cancelling have their own steps.
  public void ChangeStatus(AuctionStatus currentStatus, AuctionStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);
    EnsureOpen(currentStatus);
    status.EnsureActive();

    if (status.Is(SystemMasterCodes.Awarded) || status.Is(SystemMasterCodes.Cancelled))
      throw new DomainException("Use award / cancel for these statuses.");

    AuctionStatusId = status.Id;
  }

  public AuctionBid AddBid(AuctionStatus currentStatus, PropertyOwner bidder, decimal bidAmount, decimal? earnestMoney, string? remarks)
  {
    ArgumentNullException.ThrowIfNull(bidder);
    EnsureOpen(currentStatus);
    bidder.EnsureActive();

    if (_bids.Any(b => b.BidderOwnerId == bidder.Id))
      throw new DomainException($"{bidder.OwnerName.Value} has already bid in auction {AuctionNo.Value}.");

    var bid = AuctionBid.Create(AuctionBidId.New(), Id, bidder.Id, bidAmount, earnestMoney, remarks);
    _bids.Add(bid);
    RankBids();
    return bid;
  }

  /// Awards the auction. With recorded bids the winner is one of them (the highest by default);
  /// without bids the successful bidder and amount are given directly.
  public void Award(
      AuctionStatus currentStatus,
      AuctionStatus awardedStatus,
      AuctionBidId? winningBidId,
      PropertyOwner? successfulBidder,
      decimal? winningAmount,
      DateOnly awardDate,
      string? awardReferenceNo)
  {
    EnsureOpen(currentStatus);
    awardedStatus.EnsureIs(SystemMasterCodes.Awarded);

    OwnerId winner;
    decimal amount;

    if (_bids.Count > 0)
    {
      var bid = winningBidId is null
        ? _bids.OrderBy(b => b.BidRank).First()
        : _bids.FirstOrDefault(b => b.Id == winningBidId) ?? throw new DomainException("The winning bid does not belong to this auction.");

      _bids.ForEach(b => b.SetWinning(b.Id == bid.Id));
      winner = bid.BidderOwnerId;
      amount = bid.BidAmount;
    }
    else
    {
      if (successfulBidder is null || winningAmount is null)
        throw new DomainException("With no recorded bids, give the successful bidder and the winning amount.");

      successfulBidder.EnsureActive();
      winner = successfulBidder.Id;
      amount = Guard.Positive(winningAmount.Value, "Winning bid amount");
    }

    if (BaseReservePrice is { } reserve && amount < reserve)
      throw new DomainException($"The winning bid ({amount:N2}) is below the base reserve price ({reserve:N2}).");

    if (AuctionDate is { } held && awardDate < held)
      throw new DomainException("An auction cannot be awarded before it was held.");

    SuccessfulBidderOwnerId = winner;
    WinningBidAmount = amount;
    AwardDate = awardDate;
    AwardReferenceNo = Guard.Text(awardReferenceNo, 100, "Award reference no.");
    AuctionStatusId = awardedStatus.Id;
  }

  public void Cancel(AuctionStatus currentStatus, AuctionStatus cancelledStatus, string? remarks)
  {
    EnsureOpen(currentStatus);
    cancelledStatus.EnsureIs(SystemMasterCodes.Cancelled);
    AuctionStatusId = cancelledStatus.Id;
    Remarks = Guard.Text(remarks, 4000, "Remarks") ?? Remarks;
  }

  /// Highest bid ranks 1.
  private void RankBids()
  {
    var rank = 0;
    foreach (var bid in _bids.OrderByDescending(b => b.BidAmount).ThenBy(b => b.CreatedAt))
      bid.SetRank(++rank);
  }

  private void EnsureOpen(AuctionStatus currentStatus)
  {
    ArgumentNullException.ThrowIfNull(currentStatus);

    if (currentStatus.Id != AuctionStatusId)
      throw new DomainException("The supplied status does not match the auction.");

    if (currentStatus.Is(SystemMasterCodes.Awarded) || currentStatus.Is(SystemMasterCodes.Cancelled))
      throw new DomainException($"Auction {AuctionNo.Value} is {currentStatus.Name.Value} and closed.");
  }

  private void Apply(Details details)
  {
    ArgumentNullException.ThrowIfNull(details);
    ArgumentNullException.ThrowIfNull(details.AuctionType);

    if (details.AuctionType.Id != AuctionTypeId)
      details.AuctionType.EnsureActive();

    Guard.DateOrder(details.AnnouncementDate, details.AuctionDate, "Announcement date", "Auction date");

    AuctionTypeId = details.AuctionType.Id;
    AnnouncementDate = details.AnnouncementDate;
    AuctionDate = details.AuctionDate;
    BaseReservePrice = Guard.NotNegative(details.BaseReservePrice, "Base reserve price");
    AuctionCommitteeRef = Guard.Text(details.AuctionCommitteeRef, 100, "Auction committee ref");
    Venue = Guard.Text(details.Venue, 200, "Venue");
    ReferenceNo = Guard.Text(details.ReferenceNo, 100, "Reference no.");
    Remarks = Guard.Text(details.Remarks, 4000, "Remarks");
  }
}

/// One bid in an auction.
public class AuctionBid : Entity<AuctionBidId>
{
  public AuctionId AuctionId { get; private set; } = default!;
  public OwnerId BidderOwnerId { get; private set; } = default!;
  public decimal BidAmount { get; private set; }
  public int? BidRank { get; private set; }
  public decimal? EarnestMoney { get; private set; }
  public bool IsWinning { get; private set; }
  public string? Remarks { get; private set; }

  internal static AuctionBid Create(AuctionBidId id, AuctionId auctionId, OwnerId bidderId, decimal bidAmount, decimal? earnestMoney, string? remarks) => new()
  {
    Id = id,
    AuctionId = auctionId,
    BidderOwnerId = bidderId,
    BidAmount = Guard.Positive(bidAmount, "Bid amount"),
    EarnestMoney = Guard.NotNegative(earnestMoney, "Earnest money"),
    Remarks = Guard.Text(remarks, 300, "Remarks"),
    CreatedAt = DateTime.UtcNow
  };

  internal void SetRank(int rank) => BidRank = rank;
  internal void SetWinning(bool isWinning) => IsWinning = isWinning;
}
