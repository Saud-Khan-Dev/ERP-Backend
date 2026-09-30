using static Fixture;

public class LeaseTests
{
  private static readonly LeaseStatus Draft = Master<LeaseStatus>("DRAFT");
  private static readonly LeaseStatus Active = Master<LeaseStatus>("ACTIVE");
  private static readonly LeaseStatus Renewed = Master<LeaseStatus>("RENEWED");
  private static readonly LeaseStatus Cancelled = Master<LeaseStatus>("CANCELLED");
  private static readonly LeaseType Commercial = Master<LeaseType>("COMMERCIAL");

  private static PropertyLease.Terms Terms(string start = "2026-01-01", string end = "2030-12-31", bool renewable = true) =>
      new(Commercial, D(start), D(end), 5, null, 250000, AmountFrequency.Annual, null, null, null, renewable, null);

  private static PropertyLease NewLease(LeaseStatus status, bool renewable = true) =>
      PropertyLease.Create(LeaseId.New(), Property(), Owner("Lessee"), BusinessCode.Of("LSE-00001"), status, Terms(renewable: renewable));

  [Fact]
  public void Rule2_terms_are_editable_only_while_draft()
  {
    var lease = NewLease(Draft);
    lease.UpdateTerms(Draft, Terms(end: "2031-12-31"));
    lease.Activate(Draft, Active);

    Assert.Throws<DomainException>(() => lease.UpdateTerms(Active, Terms(end: "2032-12-31")));
  }

  [Fact]
  public void Renewal_is_a_new_row_pointing_back()
  {
    var lease = NewLease(Active);

    var renewal = lease.Renew(Active, Renewed, Active, LeaseId.New(), BusinessCode.Of("LSE-00002"), Owner("Lessee"), Terms("2031-01-01", "2035-12-31"));

    Assert.Equal(lease.Id, renewal.RenewedFromLeaseId);
    Assert.Equal(Renewed.Id, lease.LeaseStatusId);
    Assert.Equal(Active.Id, renewal.LeaseStatusId);
  }

  [Fact]
  public void Non_renewable_lease_cannot_be_renewed()
  {
    var lease = NewLease(Active, renewable: false);

    Assert.Throws<DomainException>(() =>
      lease.Renew(Active, Renewed, Active, LeaseId.New(), BusinessCode.Of("LSE-00002"), Owner(), Terms("2031-01-01", "2035-12-31")));
  }

  [Fact]
  public void Lease_must_end_after_it_starts()
  {
    Assert.Throws<DomainException>(() => PropertyLease.Create(LeaseId.New(), Property(), Owner(), BusinessCode.Of("LSE-00001"), Active,
      Terms("2026-01-01", "2025-12-31")));
  }

  [Fact]
  public void Cancelled_lease_is_no_longer_in_force()
  {
    var lease = NewLease(Active);
    lease.Cancel(Active, Cancelled, D("2026-06-01"), "s.28-A");

    Assert.False(lease.IsInForce(Cancelled));
    Assert.Throws<DomainException>(() => lease.Cancel(Cancelled, Cancelled, D("2026-07-01"), "again"));
  }
}

public class ViolationTests
{
  private static readonly AgreementType LeaseAgreement = Master<AgreementType>("LEASE");
  private static readonly AgreementType RentAgreement = Master<AgreementType>("RENT");
  private static readonly LeaseStatus Active = Master<LeaseStatus>("ACTIVE");

  private readonly Property _property = Property();
  private readonly PropertyOwner _lessee = Owner("Lessee");

  private PropertyLease Lease() => PropertyLease.Create(LeaseId.New(), _property, _lessee, BusinessCode.Of("LSE-00001"), Active,
    new PropertyLease.Terms(Master<LeaseType>("COMMERCIAL"), D("2026-01-01"), D("2030-12-31"), null, null, null, null, null, null, null, false, null));

  private AgreementViolation Record(PropertyLease lease, string date, string? deadline, IReadOnlyCollection<AgreementViolation> earlier, bool inForce = true) =>
      AgreementViolation.Record(ViolationId.New(), _property, LeaseAgreement, new AgreementViolation.Agreement(lease, null, null), _lessee,
        D(date), "Default in lease money", new AgreementViolation.Notice("N-1", D(date), deadline is null ? null : D(deadline)), earlier, inForce, null);

  [Fact]
  public void Rule5_third_violation_within_notice_period_cancels()
  {
    var lease = Lease();
    var first = Record(lease, "2026-02-01", "2026-03-31", Array.Empty<AgreementViolation>());
    var second = Record(lease, "2026-03-10", "2026-04-30", new[] { first });
    var third = Record(lease, "2026-04-15", null, new[] { first, second });

    Assert.Equal((1, false), (first.OccurrenceNo, first.LedToCancellation));
    Assert.Equal((2, false), (second.OccurrenceNo, second.LedToCancellation));
    Assert.Equal((3, true), (third.OccurrenceNo, third.LedToCancellation));
    Assert.Equal(ViolationStatus.Cancelled, third.ViolationStatus);
  }

  [Fact]
  public void Third_violation_after_the_notice_deadline_does_not_cancel()
  {
    var lease = Lease();
    var first = Record(lease, "2026-02-01", "2026-02-15", Array.Empty<AgreementViolation>());
    var second = Record(lease, "2026-03-01", "2026-03-15", new[] { first });
    var third = Record(lease, "2026-06-01", null, new[] { first, second });

    Assert.False(third.LedToCancellation);
  }

  [Fact]
  public void Agreement_no_longer_in_force_is_not_cancelled_again()
  {
    var lease = Lease();
    var first = Record(lease, "2026-02-01", "2026-12-31", Array.Empty<AgreementViolation>());
    var second = Record(lease, "2026-03-01", "2026-12-31", new[] { first });
    var third = Record(lease, "2026-04-01", null, new[] { first, second }, inForce: false);

    Assert.False(third.LedToCancellation);
  }

  [Fact]
  public void Rule4_exactly_one_matching_agreement()
  {
    var lease = Lease();

    Assert.Throws<DomainException>(() => AgreementViolation.Record(ViolationId.New(), _property, LeaseAgreement,
      new AgreementViolation.Agreement(null, null, null), _lessee, D("2026-02-01"), "x", new AgreementViolation.Notice(null, null, null),
      Array.Empty<AgreementViolation>(), true, null));

    Assert.Throws<DomainException>(() => AgreementViolation.Record(ViolationId.New(), _property, RentAgreement,
      new AgreementViolation.Agreement(lease, null, null), _lessee, D("2026-02-01"), "x", new AgreementViolation.Notice(null, null, null),
      Array.Empty<AgreementViolation>(), true, null));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1_000_000.01)]
  public void Fine_may_extend_to_one_million(decimal amount)
  {
    var violation = Record(Lease(), "2026-02-01", null, Array.Empty<AgreementViolation>());

    Assert.Throws<DomainException>(() => violation.ImposeFine(amount, Guid.NewGuid()));
  }

  [Fact]
  public void Fine_can_go_to_recovery_as_arrears()
  {
    var violation = Record(Lease(), "2026-02-01", null, Array.Empty<AgreementViolation>());
    violation.ImposeFine(50000, Guid.NewGuid());
    violation.SetFineStatus(FineStatus.RecoveryAsArrears);

    Assert.Equal((ViolationStatus.Fined, FineStatus.RecoveryAsArrears), (violation.ViolationStatus, violation.FineStatus));
  }
}

public class AuctionTests
{
  private static readonly AuctionStatus Planned = Master<AuctionStatus>("PLANNED");
  private static readonly AuctionStatus Awarded = Master<AuctionStatus>("AWARDED");

  private static PropertyAuction Auction(decimal? reserve) => PropertyAuction.Plan(AuctionId.New(), Property(), BusinessCode.Of("AUC-00001"), Planned,
    new PropertyAuction.Details(Master<AuctionType>("OPEN_AUCTION"), null, D("2026-05-01"), reserve, "AC/1", null, null, null));

  [Fact]
  public void Bids_are_ranked_and_the_highest_wins_by_default()
  {
    var auction = Auction(5_500_000);
    var low = auction.AddBid(Planned, Owner("D"), 5_000_000, null, null);
    var high = auction.AddBid(Planned, Owner("E"), 6_000_000, null, null);

    Assert.Equal((2, 1), (low.BidRank!.Value, high.BidRank!.Value));

    auction.Award(Planned, Awarded, null, null, null, D("2026-05-02"), null);

    Assert.Equal(6_000_000, auction.WinningBidAmount);
    Assert.True(high.IsWinning);
    Assert.False(low.IsWinning);
  }

  [Fact]
  public void Award_below_reserve_price_is_refused()
  {
    var auction = Auction(5_500_000);
    var low = auction.AddBid(Planned, Owner("D"), 5_000_000, null, null);

    Assert.Throws<DomainException>(() => auction.Award(Planned, Awarded, low.Id, null, null, D("2026-05-02"), null));
  }

  [Fact]
  public void Closed_auction_takes_no_bids()
  {
    var auction = Auction(null);
    auction.Award(Planned, Awarded, null, Owner("Winner"), 1_000_000, D("2026-05-02"), null);

    Assert.Throws<DomainException>(() => auction.AddBid(Awarded, Owner("Late"), 2_000_000, null, null));
  }
}
