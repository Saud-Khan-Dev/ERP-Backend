/// GDA's financial year runs from 1 July to 30 June and is named by the year it starts in: 2026 = FY 2026-27.
public static class FiscalYear
{
  public const int Earliest = 2000;
  public const int Latest = 2100;

  public static int Of(DateOnly date) => date.Month >= 7 ? date.Year : date.Year - 1;

  public static DateOnly Start(int fiscalYear) => new(fiscalYear, 7, 1);

  public static DateOnly End(int fiscalYear) => new(fiscalYear + 1, 6, 30);

  /// "2026-27"
  public static string Label(int fiscalYear) => $"{fiscalYear}-{(fiscalYear + 1) % 100:00}";

  public static int Valid(int fiscalYear, string field = "Fiscal year") =>
      fiscalYear is < Earliest or > Latest ? throw new DomainException($"{field} must be between {Earliest} and {Latest}.") : fiscalYear;

  /// The part of the year from a start date: 1 when the start is on or before 1 July, 0 when it is after 30 June,
  /// else the days left in the year over the days in the year (conservancy from registration, water from the
  /// facility date).
  public static decimal ShareFrom(int fiscalYear, DateOnly? from)
  {
    var start = Start(fiscalYear);
    var end = End(fiscalYear);
    if (from is null || from.Value <= start) return 1m;
    if (from.Value > end) return 0m;
    return (decimal)(end.DayNumber - from.Value.DayNumber + 1) / (end.DayNumber - start.DayNumber + 1);
  }
}
