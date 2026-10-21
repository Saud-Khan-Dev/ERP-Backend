/// "Today" for every date rule: the calendar date in Pakistan (Asia/Karachi, UTC+5, no daylight saving), not the UTC
/// date - between midnight and 05:00 in Pakistan the UTC date is still yesterday, so a clerk entering today's date
/// was told it was in the future.
public static class PakistanDate
{
  private static readonly TimeZoneInfo Zone = FindZone();

  public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

  /// The IANA zone where the server has the time-zone database, else a fixed UTC+5 (Pakistan has kept no daylight
  /// saving since 2009, so the two agree).
  private static TimeZoneInfo FindZone()
  {
    foreach (var id in new[] { "Asia/Karachi", "Pakistan Standard Time" })
    {
      try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
      catch (TimeZoneNotFoundException) { }
      catch (InvalidTimeZoneException) { }
    }

    return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan Standard Time", "Pakistan Standard Time");
  }
}
