using static Fixture;
using static OwnershipFixture;

/// A property's ownership from the old records (2026-10-26): owners who never had a CNIC, dates known only roughly, the
/// whole chain entered once and registered once through the transfer rules, and corrections afterwards.
public class OwnershipHistoryTests
{
  private static readonly TransferType FirstRegistration = Master<TransferType>("FIRST_REGISTRATION");
  private static readonly TransferType Inheritance = Master<TransferType>("INHERITANCE", new MasterExtras(RequiresRelationship: true));

  private static int _old;

  /// A person from the old records: no CNIC, an old NIC (or none), perhaps deceased.
  private static PropertyOwner OldOwner(string name, bool deceased = false, OldIdType type = OldIdType.OldNic, string? died = null) =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of($"OWN-9{++_old:0000}"), Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual),
        Name.Of(name), "Ghulam Rasool", null, null, null, null,
        new PropertyOwner.OldIdentity(type, type == OldIdType.NotRecorded ? null : $"101-23-45{_old:0000}"), deceased,
        died is null ? null : D(died), Today);

  private static HistoryShare S(PropertyOwner owner, decimal share) => new(owner.Id.Value, share);

  private static HistoryStep FirstOwners(string date, DatePrecision precision, params HistoryShare[] owners) =>
      new(HistoryStepKind.FirstOwners, D(date), precision, FirstRegistration.Id.Value, "Register Haqdaran 1962", null, null, null, null,
        [], owners, []);

  private static HistoryStep Transfer(string date, DatePrecision precision, TransferType type, HistoryShare[] givers, HistoryShare[] receivers,
      string? relationship = null, string? reference = "Mutation 412") =>
      new(HistoryStepKind.Transfer, D(date), precision, type.Id.Value, reference, null, relationship, null, null, givers, receivers, []);

  private static HistoryContext Context(Property property, IEnumerable<PropertyOwnership> rows, params PropertyOwner[] owners) =>
      new(property, rows.ToList(), owners.ToDictionary(o => o.Id),
        new[] { FirstRegistration, Sale, Inheritance }.ToDictionary(t => t.Id), Owned, null, Today);

  private static OwnershipHistoryEntry Enter(HistoryContext context, params HistoryStep[] steps) =>
      OwnershipHistoryEntry.Record(HistoryEntryId.New(), BusinessCode.Of("HIS-00001"), steps, null, "Clerk Estate", context, Director);

  private static IReadOnlyList<BusinessCode> Numbers(int count) =>
      Enumerable.Range(41, count).Select(n => BusinessCode.Of($"TRF-{n:00000}")).ToList();

  // ---- owners from the old records ----

  [Fact]
  public void A_person_from_the_old_records_is_entered_with_an_old_NIC_or_no_ID_instead_of_a_CNIC()
  {
    var withNic = OldOwner("Haji Fazal Karim", deceased: true, died: "1974-02-11");
    Assert.Null(withNic.Cnic);
    Assert.Equal(OldIdType.OldNic, withNic.OldIdType);
    Assert.True(withNic.IsDeceased);
    Assert.Equal(D("1974-02-11"), withNic.DateOfDeath);

    var noId = OldOwner("Mst. Zainab Bibi", type: OldIdType.NotRecorded);
    Assert.Null(noId.OldIdNo);

    var individual = Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual);
    Assert.StartsWith("An individual needs a CNIC", Message(() => PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-99001"), individual,
      Name.Of("No identity"), null, null, null, null, null)));
    Assert.StartsWith("Give the old NIC number", Message(() => PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-99002"), individual,
      Name.Of("X"), null, null, null, null, null, new PropertyOwner.OldIdentity(OldIdType.OldNic, null))));
    Assert.StartsWith("Only a person is entered with an old NIC", Message(() => PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-99003"),
      Master<OwnerType>(SystemMasterCodes.OwnerTypeCompany), Name.Of("Old Co"), null, null, null, null, null, new PropertyOwner.OldIdentity(OldIdType.NotRecorded, null))));
    Assert.StartsWith("The date of death cannot be in the future", Message(() => PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-99004"), individual,
      Name.Of("Y"), null, null, null, null, null, new PropertyOwner.OldIdentity(OldIdType.NotRecorded, null), true, Today.AddDays(1), Today)));
  }

  [Fact]
  public void A_share_registered_today_needs_a_CNIC_unless_the_holder_has_died()
  {
    var (property, _) = NewPlot();
    PropertyOwner living = OldOwner("Living, old NIC only"), dead = OldOwner("Deceased, old NIC", deceased: true);
    var record = Record(property, Array.Empty<PropertyOwnership>(), new[] { living, dead });

    Assert.Contains("has no CNIC on record - only an old NIC",
      Message(() => PropertyOwnership.RegisterFirstOwners(new[] { new PropertyOwnership.FirstOwner(living, Owned, 100, D("2001-01-01")) }, record)));

    var row = Assert.Single(PropertyOwnership.RegisterFirstOwners(new[] { new PropertyOwnership.FirstOwner(dead, Owned, 100, D("1970-01-01")) }, record));
    Assert.Equal(dead.Id, row.OwnerId);
  }

  // ---- a whole history, entered once and registered once ----

  [Fact]
  public void A_history_from_1962_registers_its_transfers_and_holdings_with_their_dates_as_known()
  {
    var (property, _) = NewPlot();
    var a = OldOwner("Haji Fazal Karim", deceased: true, died: "1980-01-01");
    var b = OldOwner("Muhammad Akram", deceased: true);
    PropertyOwner c = Owner("Kamran Mughal"), d = Owner("Nadia Mughal");
    var context = Context(property, Array.Empty<PropertyOwnership>(), a, b, c, d);

    var entry = Enter(context,
      FirstOwners("1962-06-20", DatePrecision.Year, S(a, 100)),
      Transfer("1975-03-09", DatePrecision.About, Sale, [S(a, 100)], [S(b, 100)]),
      Transfer("1998-05-03", DatePrecision.Exact, Inheritance, [S(b, 100)], [S(c, 50), S(d, 50)], relationship: "Father -> son and daughter"));

    Assert.Equal(HistoryEntryStatus.Waiting, entry.Status);
    Assert.Equal(D("1962-01-01"), entry.Steps[0].Date);
    Assert.Equal(2, entry.TransferCount);

    var registration = entry.Register(context, Numbers(2), Director);

    Assert.Equal(HistoryEntryStatus.Registered, entry.Status);
    Assert.Equal(new[] { "TRF-00041", "TRF-00042" }, registration.Transfers.Select(t => t.TransferNo.Value));
    Assert.All(registration.Transfers, t => Assert.Equal(TransferStatus.Completed, t.TransferStatus));
    Assert.Equal(DatePrecision.About, registration.Transfers[0].TransferDatePrecision);
    Assert.Empty(registration.AdoptedRows);

    var rows = registration.NewRows;
    Assert.Equal(4, rows.Count);
    var first = rows.Single(r => r.OwnerId == a.Id);
    Assert.Equal((D("1962-01-01"), DatePrecision.Year, D("1975-03-09"), OwnershipStatus.Ended),
      (first.EffectiveFrom, first.EffectiveFromPrecision, first.EffectiveTo, first.OwnershipStatus));
    Assert.Equal(DatePrecision.About, rows.Single(r => r.OwnerId == b.Id).EffectiveFromPrecision);
    Assert.Equal(new[] { c.Id, d.Id }, rows.Where(r => r.IsCurrent).Select(r => r.OwnerId));
    Assert.Equal(registration.Transfers[1].Id, rows.Single(r => r.OwnerId == c.Id).AcquiredViaTransferId);
    Assert.Equal(registration.Transfers[1].Id.Value, entry.Steps[2].RegisteredTransferId);
  }

  [Fact]
  public void A_history_must_leave_today_s_holders_with_a_CNIC()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = OldOwner("A", deceased: true), b = OldOwner("B, alive, old NIC only");
    var context = Context(property, Array.Empty<PropertyOwnership>(), a, b);

    Assert.Contains("has no CNIC on record", Message(() => Enter(context,
      FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
      Transfer("1975-01-01", DatePrecision.Year, Sale, [S(a, 100)], [S(b, 100)]))));
  }

  [Fact]
  public void A_history_ending_with_the_present_holders_gives_their_holdings_how_they_were_acquired()
  {
    var (property, _) = NewPlot();
    var a = OldOwner("Haji Fazal Karim", deceased: true);
    PropertyOwner c = Owner("Kamran Mughal"), d = Owner("Nadia Mughal");
    var present = new[] { Own(property, c, 50, "1998-05-03"), Own(property, d, 50, "1998-05-03") };
    var context = Context(property, present, a, c, d);

    // it must end with exactly the register's present holders
    Assert.Contains("must end with PROP-00001's present owners", Message(() => Enter(context,
      FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
      Transfer("1999-01-01", DatePrecision.Exact, Inheritance, [S(a, 100)], [S(c, 50), S(d, 50)], relationship: "Father -> children"))));

    var entry = Enter(context,
      FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
      Transfer("1998-05-03", DatePrecision.Exact, Inheritance, [S(a, 100)], [S(c, 50), S(d, 50)], relationship: "Father -> children"));
    // the check left the present holdings as they were
    Assert.All(present, r => Assert.Null(r.AcquiredViaTransferId));

    var registration = entry.Register(context, Numbers(1), Director);

    var transfer = Assert.Single(registration.Transfers);
    Assert.Single(registration.NewRows);
    Assert.Equal(2, registration.AdoptedRows.Count);
    Assert.All(present, r => Assert.Equal((transfer.Id, Inheritance.Id), (r.AcquiredViaTransferId, r.AcquisitionTransferTypeId)));
    Assert.Equal(new[] { present[0].Id.Value }, entry.Steps[1].RegisteredOwnershipIds!.Take(1));
  }

  [Fact]
  public void Steps_are_entered_oldest_first_and_every_refusal_names_its_step()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = OldOwner("A", deceased: true), b = Owner("B"), c = Owner("C");
    var context = Context(property, Array.Empty<PropertyOwnership>(), a, b, c);

    Assert.Contains("enter the steps oldest first", Message(() => Enter(context,
      FirstOwners("1980-01-01", DatePrecision.Year, S(a, 100)),
      Transfer("1975-01-01", DatePrecision.Year, Sale, [S(a, 100)], [S(b, 100)]))));
    Assert.StartsWith("Step 1 (the first owners, 1962): The shares add up to 60%",
      Message(() => Enter(context, FirstOwners("1962-01-01", DatePrecision.Year, S(a, 60)))));
    Assert.StartsWith("Step 2 (about 1975): Transferors give 100% but transferees receive 90%",
      Message(() => Enter(context,
        FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
        Transfer("1975-01-01", DatePrecision.About, Sale, [S(a, 100)], [S(b, 90)]))));
    Assert.StartsWith("Step 3 (1990-01-01): C is not a current owner",
      Message(() => Enter(context,
        FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
        Transfer("1975-01-01", DatePrecision.Year, Sale, [S(a, 100)], [S(b, 100)]),
        Transfer("1990-01-01", DatePrecision.Exact, Sale, [S(c, 100)], [S(b, 100)]))));
  }

  [Fact]
  public void A_share_cannot_pass_to_someone_after_their_death()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = OldOwner("A", deceased: true), dead = OldOwner("Died 1970", deceased: true, died: "1970-01-01");
    var context = Context(property, Array.Empty<PropertyOwnership>(), a, dead);

    Assert.Contains("died on 1970-01-01, before this transfer", Message(() => Enter(context,
      FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)),
      Transfer("1975-01-01", DatePrecision.Year, Sale, [S(a, 100)], [S(dead, 100)]))));
  }

  [Fact]
  public void A_waiting_history_is_rejected_or_withdrawn_with_a_reason_and_changes_nothing()
  {
    var (property, _) = NewPlot();
    var a = OldOwner("A", deceased: true);
    var context = Context(property, Array.Empty<PropertyOwnership>(), a);
    var entry = Enter(context, FirstOwners("1962-01-01", DatePrecision.Year, S(a, 100)));

    entry.Reject("Mutation 412 shows 1974, not 1975.", "Director Estate", null, Today);
    Assert.Equal((HistoryEntryStatus.Rejected, "Mutation 412 shows 1974, not 1975."), (entry.Status, entry.DecisionReason));
    Assert.Contains("only a waiting ownership history can be registered", Message(() => entry.Register(context, Numbers(0), Director)));
  }

  // ---- corrections ----

  [Fact]
  public void Re_dating_a_registered_transfer_moves_the_holdings_it_closed_and_opened()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Owner("A"), b = Owner("B");
    var rows = new List<PropertyOwnership> { Own(property, a, 100, "1990-01-01") };
    var record = Record(property, rows, new[] { a, b });
    var transfer = Initiate(record, "2005-06-01", "TRF-00007", Gives(a, 100), Gets(b, 100));
    rows.AddRange(Approve(transfer, record).Opened);
    record = Record(property, rows, new[] { a, b });

    transfer.CorrectHistory(Sale, D("2004-01-01"), DatePrecision.Year, "Mutation 88", 150000, null, "DC order 7/2004", null, record);

    Assert.Equal((D("2004-01-01"), DatePrecision.Year), (transfer.TransferDate, transfer.TransferDatePrecision));
    Assert.Equal(D("2004-01-01"), rows.Single(r => r.OwnerId == a.Id).EffectiveTo);
    var opened = rows.Single(r => r.OwnerId == b.Id);
    Assert.Equal((D("2004-01-01"), DatePrecision.Year), (opened.EffectiveFrom, opened.EffectiveFromPrecision));

    Assert.Contains("held the share given in TRF-00007 only from 1990-01-01",
      Message(() => transfer.CorrectHistory(Sale, D("1989-01-01"), DatePrecision.Exact, null, null, null, null, null, record)));
  }
}
