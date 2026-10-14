using static Fixture;
using static OwnershipFixture;

/// Allotment is not ownership; one allotment in force per GDA plot; the status machine; cancellation and
/// surrender end the ownership confirmed through the allotment; confirmation rules.
public class AllotmentRuleTests
{
  [Fact]
  public void Only_GDA_property_can_be_allotted()
  {
    var (property, classification) = NewPlot("PRIVATE_PROPERTY");

    Assert.Equal("Only GDA property can be allotted; PROP-00001 is classified as PRIVATE_PROPERTY.",
      Message(() => Allot(property, classification, Owner("Shazia Bibi"))));
  }

  [Fact]
  public void Only_one_allotment_is_in_force_per_plot()
  {
    var (property, classification) = NewPlot();
    var first = Allot(property, classification, Owner("Shazia Bibi"), no: "ALT-00037");

    Assert.Equal("ALT-00037 to Shazia Bibi is already in force on this plot; cancel, surrender or expire it first.",
      Message(() => Allot(property, classification, Owner("Other"), no: "ALT-00038", inForce: new PropertyAllotment.InForce(first, "Shazia Bibi"))));
  }

  [Fact]
  public void An_allotment_is_not_dated_in_the_future()
  {
    var (property, classification) = NewPlot();

    Assert.Contains("cannot be in the future", Message(() => Allot(property, classification, Owner(), "2026-10-14")));
  }

  [Fact]
  public void Allotment_does_not_make_an_owner_until_confirmed()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });

    Assert.Null(allotment.ConfirmedOwnership(record.Ownerships));

    var ownership = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null, record);

    Assert.Equal(allotment.Id, ownership.AcquiredViaAllotmentId);
    Assert.Equal("LTR-1", ownership.ReferenceNo);
    Assert.Same(ownership, allotment.ConfirmedOwnership(new[] { ownership }));
  }

  [Fact]
  public void Confirmation_dates_lie_between_the_allotment_and_today()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee, "2025-01-01");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });

    Assert.Contains("cannot start before the allotment date (2025-01-01)",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2024-12-31"), null, null, record)));
    Assert.Contains("cannot be in the future",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2026-10-14"), null, null, record)));
  }

  [Fact]
  public void Confirmation_is_refused_when_nothing_is_unallocated_or_already_confirmed()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var holder = Owner("B");
    var allotment = Allot(property, classification, allottee);

    var full = Record(property, new[] { Own(property, holder, 100) }, new[] { allottee, holder });
    Assert.Equal("Someone already holds 100% of PROP-00001; transfer a share to the allottee instead.",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null, full)));

    var empty = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null, empty);
    var after = Record(property, new[] { confirmed }, new[] { allottee });

    Assert.Contains("ALT-00001 was already confirmed as ownership from 2025-02-01",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-03-01"), null, null, after)));
  }

  [Fact]
  public void Cancelling_a_confirmed_allotment_ends_its_ownership_and_restoring_does_not_reopen_it()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null,
      Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }));
    var record = Record(property, new[] { confirmed }, new[] { allottee });

    var ended = allotment.Cancel(Active, Cancelled, D("2026-03-01"), "Construction deadline missed.", "ORD-12", record);

    Assert.Same(confirmed, Assert.Single(ended));
    Assert.Equal(OwnershipStatus.Ended, confirmed.OwnershipStatus);
    Assert.Equal(D("2026-03-01"), confirmed.EffectiveTo);
    Assert.EndsWith("Ended by cancellation of ALT-00001.", confirmed.Remarks);
    Assert.Equal(Cancelled.Id, allotment.AllotmentStatusId);

    allotment.Restore(property, Cancelled, Restored, D("2026-04-01"), "APPEAL-1", inForce: null, Today);

    Assert.Equal(OwnershipStatus.Ended, confirmed.OwnershipStatus);
    Assert.Null(allotment.ConfirmedOwnership(record.Ownerships));

    // the clerk confirms again, from the restoration on
    var restored = Record(property, new[] { confirmed }, new[] { allottee });
    Assert.Contains("restored on 2026-04-01",
      Message(() => allotment.ConfirmOwnership(OwnershipId.New(), Restored, allottee, Owned, 100, D("2026-03-15"), null, null, restored)));
    var again = allotment.ConfirmOwnership(OwnershipId.New(), Restored, allottee, Owned, 100, D("2026-04-01"), null, null, restored);
    Assert.Same(again, allotment.ConfirmedOwnership(new[] { confirmed, again }));
  }

  [Fact]
  public void A_cancellation_cannot_predate_the_confirmed_ownership_or_strand_a_mortgage()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee, "2025-01-01");
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-06-01"), null, null,
      Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }));

    var record = Record(property, new[] { confirmed }, new[] { allottee });
    Assert.Contains("date the cancellation on or after that",
      Message(() => allotment.Cancel(Active, Cancelled, D("2025-03-01"), "x", null, record)));

    var charged = Record(property, new[] { confirmed }, new[] { allottee }, charges: new[] { Charge(property, confirmed) });
    Assert.Contains("release it before the allotment is cancelled",
      Message(() => allotment.Cancel(Active, Cancelled, D("2026-03-01"), "x", null, charged)));
    Assert.Equal(OwnershipStatus.Active, confirmed.OwnershipStatus);
  }

  [Fact]
  public void Status_machine_in_force_to_surrendered_or_expired_never_back()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });

    Assert.Contains("restore act", Message(() => allotment.ChangeStatus(Active, Restored, null, record)));
    Assert.Contains("restore act", Message(() => allotment.ChangeStatus(Active, Active, null, record)));
    Assert.Contains("cancel act", Message(() => allotment.ChangeStatus(Active, Cancelled, null, record)));
    Assert.Contains("only be surrendered or expired", Message(() => allotment.ChangeStatus(Active, Master<AllotmentStatus>("ON_HOLD"), null, record)));
    Assert.Contains("date the allotment was surrendered", Message(() => allotment.ChangeStatus(Active, Surrendered, null, record)));
    Assert.Contains("cannot be in the future", Message(() => allotment.ChangeStatus(Active, Surrendered, D("2026-10-14"), record)));

    allotment.ChangeStatus(Active, Surrendered, D("2026-05-01"), record);
    Assert.Equal(Surrendered.Id, allotment.AllotmentStatusId);
    Assert.Equal(D("2026-05-01"), allotment.SurrenderDate);

    Assert.Contains("only an allotment in force can be cancelled",
      Message(() => allotment.Cancel(Surrendered, Cancelled, D("2026-06-01"), "x", null, record)));
    Assert.Contains("only an allotment in force", Message(() => allotment.ChangeStatus(Surrendered, Expired, D("2026-06-01"), record)));
    Assert.Contains("Only a cancelled allotment can be restored",
      Message(() => allotment.Restore(property, Surrendered, Restored, D("2026-06-01"), null, null, Today)));
  }

  [Fact]
  public void Surrender_ends_the_confirmed_ownership()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null,
      Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }));

    var ended = allotment.ChangeStatus(Active, Surrendered, D("2026-05-01"), Record(property, new[] { confirmed }, new[] { allottee }));

    Assert.Single(ended);
    Assert.Equal(D("2026-05-01"), confirmed.EffectiveTo);
    Assert.EndsWith("Ended by surrender of ALT-00001.", confirmed.Remarks);
  }

  [Fact]
  public void An_allotment_expires_only_after_its_expiry_date_or_on_a_recorded_date()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });

    var withTerm = Allot(property, classification, allottee, expiry: D("2030-01-01"));
    Assert.Equal("ALT-00001 expires on 2030-01-01; it can be marked expired after that.",
      Message(() => withTerm.ChangeStatus(Active, Expired, null, record)));

    var lapsed = Allot(property, classification, allottee, expiry: D("2026-01-01"));
    lapsed.ChangeStatus(Active, Expired, null, record);
    Assert.Equal(Expired.Id, lapsed.AllotmentStatusId);

    var openEnded = Allot(property, classification, allottee);
    Assert.Contains("give the date it expired", Message(() => openEnded.ChangeStatus(Active, Expired, null, record)));
    openEnded.ChangeStatus(Active, Expired, D("2026-09-30"), record);
    Assert.Equal(D("2026-09-30"), openEnded.ExpiryDate);
  }

  [Fact]
  public void A_cancelled_allotment_is_not_cancelled_again_and_restores_only_when_the_plot_is_free()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });
    allotment.Cancel(Active, Cancelled, D("2026-03-01"), "x", null, record);

    Assert.Equal("Allotment ALT-00001 is already cancelled.", Message(() => allotment.Cancel(Cancelled, Cancelled, D("2026-03-02"), "x", null, record)));

    var newer = Allot(property, classification, Owner("Other"), "2026-04-01", "ALT-00002");
    Assert.Contains("ALT-00002 to Other is already in force",
      Message(() => allotment.Restore(property, Cancelled, Restored, D("2026-05-01"), null, new PropertyAllotment.InForce(newer, "Other"), Today)));
    Assert.Contains("before it was cancelled",
      Message(() => allotment.Restore(property, Cancelled, Restored, D("2026-02-01"), null, null, Today)));
  }

  [Fact]
  public void A_confirmed_allotment_is_not_withdrawn_while_its_ownership_stands()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var confirmed = allotment.ConfirmOwnership(OwnershipId.New(), Active, allottee, Owned, 100, D("2025-02-01"), null, null,
      Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee }));

    Assert.Contains("cancel the allotment or end that ownership first", Message(() => allotment.Deactivate(property, new[] { confirmed })));
  }

  [Fact]
  public void Allotment_acts_are_refused_on_a_deactivated_property()
  {
    var (property, classification) = NewPlot();
    var allottee = Owner("Shazia Bibi");
    var allotment = Allot(property, classification, allottee);
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { allottee });

    property.Deactivate();

    Assert.Contains("inactive", Message(() => allotment.Cancel(Active, Cancelled, D("2026-03-01"), "x", null, record)));
    Assert.Contains("inactive", Message(() => allotment.ChangeStatus(Active, Surrendered, D("2026-03-01"), record)));
    Assert.Contains("inactive", Message(() => allotment.Update(property, Plot, D("2025-01-01"), null, null, null, null, null, Today)));
  }
}
