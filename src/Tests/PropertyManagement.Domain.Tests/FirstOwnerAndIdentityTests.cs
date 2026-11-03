using static Fixture;
using static OwnershipFixture;

/// First owners (only on a property nobody holds, exactly 100%), old-record identities and the date of death.
public class FirstOwnerAndIdentityTests
{
  private static PropertyOwnership.FirstOwner First(PropertyOwner owner, decimal share, string from = "2020-01-01") =>
      new(owner, Owned, share, D(from));

  private static OwnershipRecord Empty(Property property, params PropertyOwner[] owners) =>
      Record(property, Array.Empty<PropertyOwnership>(), owners);

  private static PropertyOwner Person(string name, PropertyOwner.OldIdentity? oldId = null, string? died = null, bool cnic = false) =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-09001"), Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual),
        Name.Of(name), null, cnic ? Cnic.Of("3520299999991") : null, null, null, null, oldId, died is null ? null : D(died), Today);

  private static readonly PropertyOwner.OldIdentity NoId = new(OldIdType.NotRecorded, null);

  // ---- first owners ----

  [Fact]
  public void First_owners_make_exactly_100_percent_as_stored()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Owner("A"), b = Owner("B"), c = Owner("C");
    var record = Empty(property, a, b, c);

    Assert.Contains("add up to 90%", Message(() => PropertyOwnership.RegisterFirstOwners([First(a, 50), First(b, 40)], record)));
    // each share keeps four decimals: 33.33334 is stored as 33.3333, so the three make 99.9999%
    Assert.Contains("add up to 99.9999%",
      Message(() => PropertyOwnership.RegisterFirstOwners([First(a, 33.33334m), First(b, 33.33333m), First(c, 33.33333m)], record)));

    var rows = PropertyOwnership.RegisterFirstOwners([First(a, 33.3334m), First(b, 33.3333m), First(c, 33.3333m)], record);
    Assert.Equal(100m, rows.Sum(r => r.OwnershipSharePct));
  }

  [Fact]
  public void First_owners_are_refused_once_anyone_holds_the_property()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Owner("A"), b = Owner("B");
    var record = Record(property, new[] { Own(property, a, 100) }, new[] { a, b });

    Assert.Contains("already held. A new owner comes in only through a transfer",
      Message(() => PropertyOwnership.RegisterFirstOwners([First(b, 100)], record)));
  }

  [Fact]
  public void An_owner_is_listed_once_and_not_dated_in_the_future()
  {
    var (property, _) = NewPlot();
    var a = Owner("A");
    var record = Empty(property, a);

    Assert.Contains("listed more than once", Message(() => PropertyOwnership.RegisterFirstOwners([First(a, 50), First(a, 50)], record)));
    Assert.Contains("cannot start in the future", Message(() => PropertyOwnership.RegisterFirstOwners([First(a, 100, "2026-12-01")], record)));
  }

  // ---- identity ----

  [Fact]
  public void An_old_identity_is_for_people_only_and_needs_its_number()
  {
    Assert.Contains("needs a CNIC", Message(() => Person("No papers")));
    Assert.Contains("Give the old NIC number", Message(() => Person("Old NIC", new PropertyOwner.OldIdentity(OldIdType.OldNic, null))));
    Assert.Contains("takes no number", Message(() => Person("No ID", new PropertyOwner.OldIdentity(OldIdType.NotRecorded, "123"))));
    Assert.Contains("cannot be in the future", Message(() => Person("Died", NoId, died: "2026-12-01")));

    var oldNic = Person("Ghulam Rasool", new PropertyOwner.OldIdentity(OldIdType.OldNic, "123-45-678901"));
    Assert.Equal(OldIdType.OldNic, oldNic.OldIdType);
    Assert.False(oldNic.CanHoldToday);
  }

  [Fact]
  public void Someone_with_no_CNIC_gets_no_share_or_agreement_today()
  {
    var (property, classification) = NewPlot();
    var oldNic = Person("Ghulam Rasool", new PropertyOwner.OldIdentity(OldIdType.OldNic, "123-45-678901"));

    Assert.Contains("has no CNIC on record", Message(() => PropertyOwnership.RegisterFirstOwners([First(oldNic, 100)], Empty(property, oldNic))));
    Assert.Contains("has no CNIC on record", Message(() => Allot(property, classification, oldNic)));
  }

  [Fact]
  public void A_holder_cannot_drop_the_CNIC_that_lets_them_hold()
  {
    var b = Owner("B");
    var individual = Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual);

    Assert.Contains("has no CNIC on record", Message(() =>
      b.Update(individual, Name.Of("B"), null, null, null, null, null, NoId, null, Today, holdsShareSince: D("2020-01-01"))));
  }

  // ---- the date of death ----

  [Fact]
  public void A_share_reaches_nobody_after_their_death()
  {
    var (property, classification) = NewPlot();
    PropertyOwner b = Owner("B");
    var dead = Person("Late Fazal", NoId, died: "2010-05-01");
    var record = Record(property, new[] { Own(property, b, 100) }, new[] { b, dead });

    Assert.Contains("died on 01/05/2010", Message(() => PropertyOwnership.RegisterFirstOwners([First(dead, 100, "2015-01-01")], Empty(property, dead))));
    Assert.Contains("died on 01/05/2010", Message(() => Initiate(record, "2026-10-01", "TRF-00090", Gives(b, 100), Gets(dead, 100))));
    Assert.Contains("cannot take on an allotment", Message(() => Allot(property, classification, dead)));

    // their estate: a first owner on record before the death stays in their name
    Assert.Single(PropertyOwnership.RegisterFirstOwners([First(dead, 100, "2001-01-01")], Empty(property, dead)));
  }

  [Fact]
  public void After_the_death_a_share_passes_only_by_inheritance_or_a_decree()
  {
    var (property, _) = NewPlot();
    var heir = Owner("Heir");
    var dead = Person("Late Fazal", new PropertyOwner.OldIdentity(OldIdType.OldNic, "11-22-33"), died: "2010-05-01");
    var record = Record(property, new[] { Own(property, dead, 100, "2001-01-01") }, new[] { dead, heir });

    Assert.Contains("passes only by inheritance or a court decree",
      Message(() => Initiate(record, "2026-10-01", "TRF-00091", Gives(dead, 100), Gets(heir, 100))));

    var inheritance = Master<TransferType>(SystemMasterCodes.TransferInheritance);
    var passed = PropertyTransfer.Initiate(TransferId.New(), BusinessCode.Of("TRF-00092"), inheritance, D("2026-10-01"), "MUT-9", null, "Son",
      null, [Gives(dead, 100), Gets(heir, 100)], record);
    Assert.Equal(TransferStatus.Initiated, passed.TransferStatus);
  }
}
