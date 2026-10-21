public class DepreciationTests
{
  private static readonly DateOnly Jan1 = new(2026, 1, 1);

  [Fact]
  public void Straight_line_spreads_the_cost_evenly_by_calendar_month()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    var schedule = Fixture.Schedule(120_000m, 12, Jan1, method: method);
    var entries = schedule.GenerateEntries(method, new DateOnly(2026, 12, 31));

    Assert.Equal(12, entries.Count);
    Assert.All(entries, e => Assert.Equal(10_000m, e.DepreciationAmount));
    Assert.Equal(new DateOnly(2026, 1, 31), entries[0].LastDay);
    Assert.Equal(0m, entries[^1].BookValueAfter);
  }

  [Fact]
  public void A_part_month_takes_its_share_of_days_and_the_last_month_takes_what_is_left()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    var schedule = Fixture.Schedule(31_000m, 1, new DateOnly(2026, 1, 16), method: method);
    var entries = schedule.GenerateEntries(method, new DateOnly(2026, 3, 1));

    Assert.Equal(16_000m, entries[0].DepreciationAmount); // 16 of January's 31 days
    Assert.Equal(31_000m, entries.Sum(e => e.DepreciationAmount));
  }

  [Fact]
  public void Declining_balance_never_goes_below_the_salvage_value()
  {
    var method = Fixture.Method(DepreciationMethodKind.DecliningBalance);
    var schedule = Fixture.Schedule(100_000m, 36, Jan1, salvage: 10_000m, rate: 0.4m, method: method);
    var entries = schedule.GenerateEntries(method, new DateOnly(2030, 1, 1));

    Assert.Equal(90_000m, entries.Sum(e => e.DepreciationAmount));
    Assert.Equal(10_000m, entries[^1].BookValueAfter);
    Assert.True(entries[0].DepreciationAmount > entries[^1].DepreciationAmount);
  }

  [Fact]
  public void A_month_that_has_not_ended_cannot_be_posted()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    var schedule = Fixture.Schedule(120_000m, 12, Jan1, method: method);
    var entries = schedule.GenerateEntries(method, new DateOnly(2026, 2, 10));

    var february = entries.Single(e => e.PeriodStart == new DateOnly(2026, 2, 1));
    Assert.Throws<DomainException>(() => schedule.PostEntry(february.Id, Fixture.Clerk, DateTime.UtcNow, new DateOnly(2026, 2, 27)));
    schedule.PostEntry(february.Id, Fixture.Clerk, DateTime.UtcNow, new DateOnly(2026, 2, 28)); // its last day: over at day's end
    Assert.True(february.Posted);
    Assert.Equal("Clerk One", february.PostedByName);
  }

  [Fact]
  public void Posting_up_to_a_date_posts_only_the_months_ended_by_then()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    var schedule = Fixture.Schedule(120_000m, 12, Jan1, method: method);
    schedule.GenerateEntries(method, new DateOnly(2026, 6, 15));

    var posted = schedule.PostEndedEntries(new DateOnly(2026, 12, 31), Fixture.Clerk, DateTime.UtcNow, new DateOnly(2026, 6, 15));
    Assert.Equal(5, posted);
    Assert.Equal(new DateOnly(2026, 5, 31), schedule.PostedThrough);
  }

  [Fact]
  public void Book_value_is_cost_less_every_posted_month_not_reversed_across_all_schedules()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    var first = Fixture.Schedule(120_000m, 12, Jan1, method: method);
    first.GenerateEntries(method, new DateOnly(2026, 3, 31));
    first.PostEndedEntries(new DateOnly(2026, 3, 31), Fixture.Clerk, DateTime.UtcNow, new DateOnly(2026, 4, 1));
    var march = first.Entries.Single(e => e.PeriodStart == new DateOnly(2026, 3, 1));
    first.ReverseEntry(march.Id, Fixture.Clerk, DateTime.UtcNow);

    // a second schedule (a revised life) from the book value left
    var second = Fixture.Schedule(100_000m, 10, new DateOnly(2026, 3, 1), method: method);
    second.GenerateEntries(method, new DateOnly(2026, 3, 31));
    second.PostEndedEntries(new DateOnly(2026, 3, 31), Fixture.Clerk, DateTime.UtcNow, new DateOnly(2026, 4, 1));

    Assert.Equal(30_000m, AssetBookValue.WrittenOff([first, second], new DateOnly(2026, 4, 1)));
    Assert.Equal(90_000m, AssetBookValue.NetBookValue(120_000m, [first, second], new DateOnly(2026, 4, 1)));
    Assert.Equal(110_000m, AssetBookValue.NetBookValue(120_000m, [first, second], new DateOnly(2026, 2, 15)));
  }

  [Fact]
  public void Salvage_above_the_starting_value_is_refused()
  {
    Assert.Throws<DomainException>(() => Fixture.Schedule(10_000m, 12, Jan1, salvage: 12_000m));
  }

  [Fact]
  public void A_system_method_keeps_its_code_and_calculation()
  {
    var method = Fixture.Method(DepreciationMethodKind.StraightLine);
    Assert.Throws<DomainException>(() => method.Update(LookupCode.Of("SL2"), method.Name, null, DepreciationMethodKind.StraightLine, true, false));
    Assert.Throws<DomainException>(() => method.Update(method.Code, method.Name, null, DepreciationMethodKind.DecliningBalance, true, false));
    method.Update(method.Code, Name.Of("Straight-line"), "renamed", DepreciationMethodKind.StraightLine, true, false);
    Assert.Equal("Straight-line", method.Name.Value);
  }
}
