using static Fixture;
using static OwnershipFixture;

/// An ownership history from the old records: played through the register's rules, vague dates kept in order,
/// today's holders with a CNIC, a dead owner's share by inheritance, and a corrected transfer moving its rows.
public class OwnershipHistoryTests
{
  private static readonly TransferType Inheritance = Master<TransferType>(SystemMasterCodes.TransferInheritance);

  private static PropertyOwner Person(string name, string? died = null, bool cnic = false) =>
      PropertyOwner.Create(OwnerId.New(), BusinessCode.Of("OWN-09002"), Master<OwnerType>(SystemMasterCodes.OwnerTypeIndividual), Name.Of(name), null,
        cnic ? Cnic.Of($"3520{Random.Shared.Next(100000000, 999999999)}") : null, null, null, null,
        cnic ? null : new PropertyOwner.OldIdentity(OldIdType.NotRecorded, null), died is null ? null : D(died), Today);

  private static HistoryContext Context(Property property, params PropertyOwner[] owners) => new(property, owners.ToDictionary(o => o.Id),
    new Dictionary<MasterId, TransferType> { [Sale.Id] = Sale, [Inheritance.Id] = Inheritance }, Owned, Director, "clerk1", Today);

  private static HistoryStep First(string date, DatePrecision precision, params (PropertyOwner Owner, decimal Pct)[] owners) =>
      new(HistoryStepKind.FirstOwners, D(date), precision, null, "REG-1", null, [], owners.Select(o => new HistoryShare(o.Owner.Id.Value, o.Pct)).ToList());

  private static HistoryStep Pass(string date, DatePrecision precision, TransferType type, PropertyOwner from, PropertyOwner to, decimal pct = 100) =>
      new(HistoryStepKind.Transfer, D(date), precision, type.Id.Value, "MUT-1", type == Inheritance ? "Father -> Son" : null,
        [new HistoryShare(from.Id.Value, pct)], [new HistoryShare(to.Id.Value, pct)]);

  private static HistoryRegistration Play(HistoryContext context, params HistoryStep[] steps) =>
      HistoryPlayer.Play(HistoryPlayer.Normalize(steps), context, n => BusinessCode.Of($"TRF-{n:00000}"), HistoryEntryId.New());

  [Fact]
  public void A_history_plays_through_to_today_s_holders()
  {
    var (property, _) = NewPlot();
    var a = Person("Ghulam Rasool");
    var b = Owner("Bashir");
    var result = Play(Context(property, a, b), First("1962-05-01", DatePrecision.Year, (a, 100)), Pass("1975-01-01", DatePrecision.Year, Sale, a, b));

    var transfer = Assert.Single(result.Transfers);
    Assert.Equal(D("1975-01-01"), transfer.TransferDate);
    Assert.Equal(DatePrecision.Year, transfer.TransferDatePrecision);
    var current = Assert.Single(result.Rows, r => r.IsCurrent);
    Assert.Equal(b.Id, current.OwnerId);
    Assert.Equal(transfer.Id, current.OpenedByTransferId);
    Assert.Equal(transfer.Id, Assert.Single(result.Rows, r => r.OwnerId == a.Id).ClosedByTransferId);
    Assert.Equal(DatePrecision.Year, result.Rows.Single(r => r.OwnerId == a.Id).EffectiveFromPrecision);
  }

  [Fact]
  public void A_vague_date_never_falls_before_the_step_before_it_but_an_exact_one_must_come_after()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Person("A"), b = Person("B"), c = Owner("C");
    var steps = HistoryPlayer.Normalize([First("1972-03-14", DatePrecision.Exact, (a, 100)), Pass("1972-01-01", DatePrecision.Year, Sale, a, b),
      Pass("1972-01-01", DatePrecision.Year, Sale, b, c)]);
    Assert.Equal(D("1972-03-15"), steps[1].Date);
    Assert.Equal(D("1972-03-16"), steps[2].Date);
    Assert.Equal(2, Play(Context(property, a, b, c), [.. steps]).Transfers.Count);

    Assert.Contains("is not after the step before it",
      Message(() => HistoryPlayer.Normalize([First("1980-01-01", DatePrecision.Exact, (a, 100)), Pass("1979-06-01", DatePrecision.Exact, Sale, a, c)])));
  }

  [Fact]
  public void Whoever_holds_a_share_today_needs_a_CNIC()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Person("A"), b = Person("B");
    Assert.Contains("B (OWN-09002) has no CNIC on record",
      Message(() => Play(Context(property, a, b), First("1962-01-01", DatePrecision.Year, (a, 100)), Pass("1975-01-01", DatePrecision.Year, Sale, a, b))));
  }

  [Fact]
  public void After_a_death_the_share_passes_only_by_inheritance()
  {
    var (property, _) = NewPlot();
    var father = Person("Late Fazal", died: "1980-06-01");
    var son = Owner("Son");
    Assert.Contains("Step 2: Late Fazal died on 01/06/1980",
      Message(() => Play(Context(property, father, son), First("1962-01-01", DatePrecision.Year, (father, 100)), Pass("1985-01-01", DatePrecision.Year, Sale, father, son))));
    Assert.Single(Play(Context(property, father, son), First("1962-01-01", DatePrecision.Year, (father, 100)),
      Pass("1985-01-01", DatePrecision.Year, Inheritance, father, son)).Transfers);
  }

  [Fact]
  public void An_entry_waits_is_sent_back_put_right_and_is_refused_once_the_property_is_held()
  {
    var (property, _) = NewPlot();
    var a = Owner("A");
    var context = Context(property, a);
    var entry = OwnershipHistoryEntry.Record(HistoryEntryId.New(), BusinessCode.Of("HIS-00001"), [First("1990-01-01", DatePrecision.Exact, (a, 100))], null, Guid.NewGuid(), context, false);
    Assert.Equal(HistoryEntryStatus.Waiting, entry.Status);

    entry.Reject(Director, Today, "The register shows 1991.");
    Assert.Equal(HistoryEntryStatus.Rejected, entry.Status);
    entry.Revise([First("1991-01-01", DatePrecision.Year, (a, 100))], null, Guid.NewGuid(), context, false);
    Assert.Equal(HistoryEntryStatus.Waiting, entry.Status);
    Assert.Null(entry.DecisionReason);

    Assert.Contains("already has owners on the register", Message(() => entry.Register(context, true, n => BusinessCode.Of($"TRF-{n:00000}"))));
    Assert.Single(entry.Register(context, false, n => BusinessCode.Of($"TRF-{n:00000}")).Rows);
    Assert.Equal(HistoryEntryStatus.Registered, entry.Status);
  }

  [Fact]
  public void Correcting_a_transfer_from_the_old_records_moves_its_rows_and_stays_between_its_neighbours()
  {
    var (property, _) = NewPlot();
    PropertyOwner a = Person("A"), b = Owner("B"), c = Owner("C");
    var result = Play(Context(property, a, b, c), First("1962-01-01", DatePrecision.Year, (a, 100)),
      Pass("1975-01-01", DatePrecision.Year, Sale, a, b), Pass("1990-01-01", DatePrecision.Year, Sale, b, c));
    var first = result.Transfers[0];
    var record = Record(property, result.Rows, new[] { a, b, c });

    Assert.Contains("must stay before the step after it",
      Message(() => first.CorrectHistory(Sale, D("1991-01-01"), DatePrecision.Year, null, null, record, D("1962-01-01"), D("1990-01-01"))));

    first.CorrectHistory(Sale, D("1976-04-01"), DatePrecision.Month, "MUT-77", null, record, D("1962-01-01"), D("1990-01-01"));
    Assert.Equal(D("1976-04-01"), result.Rows.Single(r => r.OwnerId == a.Id).EffectiveTo);
    Assert.Equal(D("1976-04-01"), result.Rows.Single(r => r.OwnerId == b.Id).EffectiveFrom);
    Assert.Equal(DatePrecision.Month, result.Rows.Single(r => r.OwnerId == b.Id).EffectiveFromPrecision);
    Assert.Equal("MUT-77", first.TransferReferenceNo);
  }
}
