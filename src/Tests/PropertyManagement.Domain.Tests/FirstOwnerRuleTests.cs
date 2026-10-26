using static Fixture;
using static OwnershipFixture;

/// The 2026-10-26 rule: a property's owners are entered directly only while NOBODY holds it - all of them at once, joint
/// owners together, with shares that make exactly 100%. Once it is held, a new owner comes in only through a transfer,
/// and there is no act that ends a share by hand.
public class FirstOwnerRuleTests
{
  private static PropertyOwnership.FirstOwner First(PropertyOwner owner, decimal share, string from = "2016-03-10", string? reference = null) =>
      new(owner, Owned, share, D(from), Sale, reference);

  [Fact]
  public void A_single_first_owner_of_an_ownerless_property_holds_it_whole()
  {
    var (property, _) = NewPlot();
    var b = Owner("Tariq Abbasi");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { b });

    var row = Assert.Single(PropertyOwnership.RegisterFirstOwners(new[] { First(b, 100, reference: "Mutation No. 1588") }, record));

    Assert.Equal((b.Id, 100m, D("2016-03-10"), OwnershipStatus.Active), (row.OwnerId, row.OwnershipSharePct, row.EffectiveFrom, row.OwnershipStatus));
    Assert.Equal("Mutation No. 1588", row.ReferenceNo);
    Assert.Equal(Sale.Id, row.AcquisitionTransferTypeId);
    Assert.Equal(property.Id, row.PropertyId);
  }

  [Fact]
  public void Joint_first_owners_are_entered_together_and_hold_exactly_100_percent()
  {
    var (property, _) = NewPlot();
    PropertyOwner tariq = Owner("Tariq Abbasi"), aamir = Owner("Aamir Abbasi");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { tariq, aamir });

    var rows = PropertyOwnership.RegisterFirstOwners(new[] { First(tariq, 50), First(aamir, 50) }, record);

    Assert.Equal(2, rows.Count);
    Assert.Equal(100, OwnershipShares.Total(rows));
    Assert.Equal(new[] { tariq.Id, aamir.Id }, rows.Select(r => r.OwnerId));
    Assert.All(rows, r => Assert.True(r.IsCurrent));
  }

  [Fact]
  public void First_owners_must_hold_exactly_100_percent_between_them()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C"), e = Owner("E");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { b, c, e });

    Assert.StartsWith("The shares add up to 60%, so 40% would have no owner. Enter every owner now",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 60) }, record)));
    Assert.StartsWith("The shares add up to 110%; together the owners hold exactly 100% of the property.",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 70), First(c, 40) }, record)));

    // a third each, to four places, is 99.9999%: one of the three is given 33.3334
    Assert.Contains("99.9999%",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 33.3333m), First(c, 33.3333m), First(e, 33.3333m) }, record)));
    var thirds = PropertyOwnership.RegisterFirstOwners(new[] { First(b, 33.3333m), First(c, 33.3333m), First(e, 33.3334m) }, record);
    Assert.Equal(100, OwnershipShares.Total(thirds));
  }

  [Fact]
  public void Owners_are_not_added_once_the_property_is_held_a_transfer_brings_them_in()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C"), d = Owner("D");
    var record = Record(property, new[] { Own(property, b, 60), Own(property, c, 40) }, new[] { b, c, d });

    Assert.Equal(
      "PROP-00001 is already registered in the name of B (60%) and C (40%). A new owner can only come in through a transfer: "
      + "use \"Start transfer\", or \"Record a completed transfer\" for one that already happened on paper.",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(d, 100, "2026-01-01") }, record)));
  }

  [Fact]
  public void A_partly_held_or_disputed_property_is_held_too()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), d = Owner("D");
    var ownB = Own(property, b, 60);
    ownB.MarkDisputed(property, "Heirs contest the will.");
    var record = Record(property, new[] { ownB }, new[] { b, d });

    Assert.Contains("is already registered in the name of B (60%)",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(d, 40, "2026-01-01") }, record)));
  }

  [Fact]
  public void Each_first_owner_is_listed_once_and_the_list_is_not_empty()
  {
    var (property, _) = NewPlot();
    var b = Owner("B");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { b });

    Assert.Equal($"B ({b.OwnerCode.Value}) is listed more than once. Give each owner one row with their whole share.",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 50), First(b, 50) }, record)));
    Assert.Equal("Give at least one owner, with the share they hold.",
      Message(() => PropertyOwnership.RegisterFirstOwners(Array.Empty<PropertyOwnership.FirstOwner>(), record)));
  }

  [Fact]
  public void A_first_owner_does_not_hold_from_a_future_date()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { b, c });

    Assert.Contains("cannot start in the future",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 50), First(c, 50, "2026-10-14") }, record)));
  }

  [Fact]
  public void An_ownerless_property_held_before_takes_owners_after_their_earlier_entries()
  {
    var (property, _) = NewPlot();
    PropertyOwner b = Owner("B"), c = Owner("C");
    var ended = Own(property, b, 100, "2015-01-01");
    ended.End(D("2025-06-30"), "Ended by cancellation of ALT-00001.");
    var record = Record(property, new[] { ended }, new[] { b, c });

    Assert.True(record.HasHolderHistory);
    Assert.Contains("B's earlier ownership runs until 2025-06-30",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(b, 100, "2025-01-01") }, record)));

    var rows = PropertyOwnership.RegisterFirstOwners(new[] { First(b, 50, "2025-07-01"), First(c, 50, "2025-07-01") }, record);
    Assert.Equal(100, OwnershipShares.Total(rows));
  }

  [Fact]
  public void After_the_first_owners_a_transfer_still_moves_a_share_to_a_new_owner()
  {
    var (property, _) = NewPlot();
    PropertyOwner tariq = Owner("Tariq"), aamir = Owner("Aamir"), d = Owner("D");
    var first = PropertyOwnership.RegisterFirstOwners(new[] { First(tariq, 50), First(aamir, 50) },
      Record(property, Array.Empty<PropertyOwnership>(), new[] { tariq, aamir }));
    var record = Record(property, first, new[] { tariq, aamir, d });

    var change = Approve(Initiate(record, "2026-10-01", "TRF-00090", Gives(aamir, 50), Gets(d, 50)), record);

    var rows = record.Ownerships.Concat(change.Opened).ToList();
    Assert.Equal(100, OwnershipShares.Total(rows));
    Assert.Equal(50, rows.Single(o => o.OwnerId == d.Id && o.IsCurrent).OwnershipSharePct);
    Assert.Equal(OwnershipStatus.Ended, first.Single(o => o.OwnerId == aamir.Id).OwnershipStatus);
    Assert.Contains("only come in through a transfer",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { First(aamir, 100, "2026-10-05") }, Record(property, rows, new[] { tariq, aamir, d }))));
  }

  [Fact]
  public void There_is_no_act_that_ends_a_share_by_hand()
  {
    // a share ends through a transfer, or through the cancellation / surrender of the allotment it was confirmed by
    var acts = typeof(PropertyOwnership).GetMethods().Where(m => m.IsPublic && !m.IsStatic).Select(m => m.Name).ToList();
    Assert.DoesNotContain("EndHolding", acts);
    Assert.DoesNotContain(typeof(PropertyOwnership).GetMethods(), m => m.Name == "AddOwner");
  }
}
