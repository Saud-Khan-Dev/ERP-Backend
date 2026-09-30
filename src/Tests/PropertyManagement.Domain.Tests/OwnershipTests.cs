using static Fixture;

public class OwnershipTests
{
  private static readonly TenureType Owned = Master<TenureType>("OWNED");

  private static PropertyOwnership Own(Property property, PropertyOwner owner, decimal share, string from = "2020-01-01") =>
      PropertyOwnership.Register(OwnershipId.New(), property, owner, Owned, share, D(from), null, null, null);

  [Fact]
  public void Rule1_active_shares_may_not_exceed_100()
  {
    var property = Property();
    var current = new[] { Own(property, Owner(), 60) };

    Assert.Throws<DomainException>(() => OwnershipShares.EnsureRoomFor(current, 50));
    OwnershipShares.EnsureRoomFor(current, 40);
  }

  [Fact]
  public void Disputed_and_ended_shares_do_not_count()
  {
    var property = Property();
    var disputed = Own(property, Owner(), 60);
    disputed.MarkDisputed(null);

    Assert.Equal(0, OwnershipShares.Total(new[] { disputed }));
  }

  [Fact]
  public void Share_must_be_above_zero_and_at_most_100()
  {
    Assert.Throws<DomainException>(() => Own(Property(), Owner(), 0));
    Assert.Throws<DomainException>(() => Own(Property(), Owner(), 100.5m));
  }

  [Fact]
  public void Ownership_cannot_end_before_it_started()
  {
    var ownership = Own(Property(), Owner(), 100, "2024-01-01");

    Assert.Throws<DomainException>(() => ownership.End(D("2023-12-31")));
  }

  [Fact]
  public void Allotment_confirmation_links_the_ownership_to_the_allotment()
  {
    var property = Property();
    var allottee = Owner("Allottee");
    var allotment = PropertyAllotment.Allot(AllotmentId.New(), property, allottee, BusinessCode.Of("ALT-00001"),
      Master<AllotmentType>("PLOT"), Master<AllotmentStatus>("ACTIVE"), D("2026-01-01"), null, null, null, null, null);

    var ownership = PropertyOwnership.FromAllotment(OwnershipId.New(), property, allotment, allottee, Owned, 100, D("2026-02-01"), null, null);

    Assert.Equal(allotment.Id, ownership.AcquiredViaAllotmentId);
    Assert.Throws<DomainException>(() =>
      PropertyOwnership.FromAllotment(OwnershipId.New(), property, allotment, Owner("Someone else"), Owned, 100, D("2026-02-01"), null, null));
  }
}

public class TransferTests
{
  private static readonly TenureType Owned = Master<TenureType>("OWNED");
  private static readonly TransferType Sale = Master<TransferType>("SALE");
  private static readonly TransferType Gift = Master<TransferType>("GIFT", new MasterExtras(RequiresRelationship: true));

  private static PropertyTransfer.PartyInput Party(PropertyOwner owner, TransferPartyRole role, decimal share) => new(owner.Id, role, share);

  [Fact]
  public void Guide_example_B_plus_C_to_C_plus_D_moves_net_shares_and_keeps_history()
  {
    var property = Property();
    PropertyOwner b = Owner("B"), c = Owner("C"), d = Owner("D");
    var ownB = PropertyOwnership.Register(OwnershipId.New(), property, b, Owned, 60, D("2020-01-01"), null, null, null);
    var ownC = PropertyOwnership.Register(OwnershipId.New(), property, c, Owned, 40, D("2020-01-01"), null, null, null);
    var current = new[] { ownB, ownC };

    var transfer = PropertyTransfer.Initiate(TransferId.New(), property, BusinessCode.Of("TRF-00001"), Sale, D("2026-07-01"),
      "MUT-1", null, null, null,
      new[] { Party(b, TransferPartyRole.Transferor, 60), Party(c, TransferPartyRole.Transferor, 20),
              Party(c, TransferPartyRole.Transferee, 30), Party(d, TransferPartyRole.Transferee, 50) },
      current);

    Assert.Equal(80, transfer.ShareTransferredPct);
    Assert.Throws<DomainException>(() => transfer.Complete(current, Owned)); // not approved yet

    transfer.Approve("officer", D("2026-07-02"));
    var change = transfer.Complete(current, Owned);

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
    var property = Property();
    var b = Owner("B");
    var current = new[] { PropertyOwnership.Register(OwnershipId.New(), property, b, Owned, 100, D("2020-01-01"), null, null, null) };

    Assert.Throws<DomainException>(() => PropertyTransfer.Initiate(TransferId.New(), property, BusinessCode.Of("TRF-00001"), Gift,
      D("2026-01-01"), null, null, null, null,
      new[] { Party(b, TransferPartyRole.Transferor, 100), Party(Owner("Son"), TransferPartyRole.Transferee, 100) }, current));
  }

  [Theory]
  [InlineData(70, 70)] // gives more than held
  [InlineData(60, 50)] // sides do not match
  public void Invalid_share_movements_are_refused(decimal given, decimal received)
  {
    var property = Property();
    var b = Owner("B");
    var current = new[] { PropertyOwnership.Register(OwnershipId.New(), property, b, Owned, 60, D("2020-01-01"), null, null, null) };

    Assert.Throws<DomainException>(() => PropertyTransfer.Initiate(TransferId.New(), property, BusinessCode.Of("TRF-00001"), Sale,
      D("2026-01-01"), null, null, null, null,
      new[] { Party(b, TransferPartyRole.Transferor, given), Party(Owner("D"), TransferPartyRole.Transferee, received) }, current));
  }

  [Fact]
  public void A_non_owner_cannot_transfer()
  {
    Assert.Throws<DomainException>(() => PropertyTransfer.Initiate(TransferId.New(), Property(), BusinessCode.Of("TRF-00001"), Sale,
      D("2026-01-01"), null, null, null, null,
      new[] { Party(Owner("Stranger"), TransferPartyRole.Transferor, 10), Party(Owner("D"), TransferPartyRole.Transferee, 10) },
      Array.Empty<PropertyOwnership>()));
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
