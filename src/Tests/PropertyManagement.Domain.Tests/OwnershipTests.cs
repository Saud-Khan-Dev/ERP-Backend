using static Fixture;
using static OwnershipFixture;

public class OwnershipTests
{
  [Fact]
  public void Rule1_active_shares_may_not_exceed_100()
  {
    var (property, _) = NewPlot();
    var current = new[] { Own(property, Owner(), 60) };

    Assert.Throws<DomainException>(() => OwnershipShares.EnsureRoomFor(current, 50));
    OwnershipShares.EnsureRoomFor(current, 40);
  }

  [Fact]
  public void Rule1_a_disputed_share_is_still_held_and_counts_toward_100()
  {
    var (property, _) = NewPlot();
    var disputed = Own(property, Owner(), 60);
    disputed.MarkDisputed(property, "Heirs contest the will.");

    Assert.True(disputed.IsCurrent);
    Assert.False(disputed.IsTransferable);
    Assert.Equal(60, OwnershipShares.Total(new[] { disputed }));
    Assert.Throws<DomainException>(() => OwnershipShares.EnsureRoomFor(new[] { disputed }, 50));
  }

  [Fact]
  public void Ended_shares_do_not_count()
  {
    var (property, _) = NewPlot();
    var ended = Own(property, Owner(), 60);
    ended.End(D("2024-01-01"));

    Assert.Equal(0, OwnershipShares.Total(new[] { ended }));
  }

  [Fact]
  public void Share_must_be_above_zero_and_at_most_100()
  {
    var (property, _) = NewPlot();
    Assert.Throws<DomainException>(() => Own(property, Owner(), 0));
    Assert.Throws<DomainException>(() => Own(property, Owner(), 100.5m));
  }

  [Fact]
  public void Ownership_cannot_end_before_it_started()
  {
    var (property, _) = NewPlot();
    var ownership = Own(property, Owner(), 100, "2024-01-01");

    Assert.Throws<DomainException>(() => ownership.End(D("2023-12-31")));
  }

  [Fact]
  public void Allotment_confirmation_links_the_ownership_to_the_allotment()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Allottee");
    var allotment = Allot(property, classification, allottee, "2026-01-01");

    var ownership = PropertyOwnership.FromAllotment(OwnershipId.New(), property, allotment, allottee, Owned, 100, D("2026-02-01"), null, null);

    Assert.Equal(allotment.Id, ownership.AcquiredViaAllotmentId);
    Assert.Throws<DomainException>(() =>
      PropertyOwnership.FromAllotment(OwnershipId.New(), property, allotment, Owner("Someone else"), Owned, 100, D("2026-02-01"), null, null));
  }
}

public class TransferTests
{
  private static readonly TransferType Gift = Master<TransferType>("GIFT", new MasterExtras(RequiresRelationship: true));

  [Fact]
  public void Guide_example_B_plus_C_to_C_plus_D_moves_net_shares_and_keeps_history()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C"), d = Owner("D");
    var ownB = Own(property, b, 60);
    var ownC = Own(property, c, 40);
    var record = Record(property, new[] { ownB, ownC }, new[] { b, c, d });

    var transfer = Initiate(record, "2026-07-01", "TRF-00001", Gives(b, 60), Gives(c, 20), Gets(c, 30), Gets(d, 50));
    Assert.Equal(80, transfer.ShareTransferredPct);

    var change = Approve(transfer, record, "2026-07-02");

    Assert.Equal(2, change.Closed.Count);
    Assert.All(change.Closed, o => Assert.Equal(OwnershipStatus.Ended, o.OwnershipStatus));
    Assert.All(change.Closed, o => Assert.Equal(D("2026-07-01"), o.EffectiveTo));
    Assert.Equal(50, change.Opened.Single(o => o.OwnerId == c.Id).OwnershipSharePct);
    Assert.Equal(50, change.Opened.Single(o => o.OwnerId == d.Id).OwnershipSharePct);
    Assert.All(change.Opened, o => Assert.Equal(transfer.Id, o.AcquiredViaTransferId));
    Assert.Equal(TransferStatus.Completed, transfer.TransferStatus);
  }

  [Fact]
  public void Gift_needs_the_relationship()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), son = Owner("Son");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, son });

    Assert.Throws<DomainException>(() => PropertyTransfer.Initiate(TransferId.New(), BusinessCode.Of("TRF-00001"), Gift,
      D("2026-01-01"), null, null, null, null, new[] { Gives(b, 100), Gets(son, 100) }, record));
  }

  [Theory]
  [InlineData(70, 70)] // gives more than held
  [InlineData(60, 50)] // sides do not match
  public void Invalid_share_movements_are_refused(decimal given, decimal received)
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 60) }, new[] { b, d });

    Assert.Throws<DomainException>(() => Initiate(record, "2026-01-01", "TRF-00001", Gives(b, given), Gets(d, received)));
  }

  [Fact]
  public void A_non_owner_cannot_transfer()
  {
    var (property, _) = NewPlot();
    PropertyOwner stranger = Owner("Stranger"), d = Owner("D");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { stranger, d });

    var message = Message(() => Initiate(record, "2026-01-01", "TRF-00001", Gives(stranger, 10), Gets(d, 10)));
    Assert.Contains("not a current owner", message);
  }
}

public class OwnerTests
{
  private static readonly ContactType Mobile = Master<ContactType>("MOBILE");

  [Fact]
  public void Cnic_is_normalized_to_the_dashed_form()
  {
    Assert.Equal("12345-1234567-1", Cnic.Of("1234512345671").Value);
    Assert.Throws<DomainException>(() => Cnic.Of("12345"));
  }

  [Fact]
  public void Rule3_first_contact_is_primary_and_only_one_stays_primary()
  {
    var owner = Owner();
    var first = owner.AddContact(Mobile, "0300-1111111", false, null);
    var second = owner.AddContact(Mobile, "0300-2222222", true, null);

    Assert.False(first.IsPrimary);
    Assert.True(second.IsPrimary);
    Assert.Single(owner.Contacts, c => c.IsPrimary);
  }

  [Fact]
  public void Deactivating_the_primary_contact_hands_the_flag_on()
  {
    var owner = Owner();
    var first = owner.AddContact(Mobile, "0300-1111111", true, null);
    var second = owner.AddContact(Mobile, "0300-2222222", false, null);

    owner.DeactivateContact(first.Id);

    Assert.True(second.IsPrimary);
    Assert.Single(owner.Contacts, c => c.IsPrimary);
  }

  [Fact]
  public void Rule3_one_primary_address()
  {
    var owner = Owner();
    owner.AddAddress(AddressType.Permanent, "House 1", null, null, null, null, null, false);
    owner.AddAddress(AddressType.Present, "House 2", null, null, null, null, null, true);

    Assert.Single(owner.Addresses, a => a.IsPrimary);
    Assert.Equal("Pakistan", owner.Addresses[0].Country);
  }
}
