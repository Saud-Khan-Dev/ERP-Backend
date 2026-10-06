/// "Today" for business dates. GDA works on Pakistan Standard Time, so a date taken from UTC would be a day behind
/// between midnight and 05:00 PKT.
public interface IClock
{
  DateTime UtcNow { get; }
  DateOnly Today { get; }
}

public sealed class PakistanClock(TimeProvider timeProvider) : IClock
{
  private static readonly TimeZoneInfo Zone = FindZone();

  public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

  public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, Zone));

  private static TimeZoneInfo FindZone()
  {
    try
    {
      return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
    }
    catch (TimeZoneNotFoundException)
    {
      // a container without tzdata: Pakistan has no daylight saving
      return TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan Standard Time", "PKT");
    }
  }
}
