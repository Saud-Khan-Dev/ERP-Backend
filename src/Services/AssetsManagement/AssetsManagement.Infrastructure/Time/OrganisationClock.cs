using Microsoft.Extensions.Configuration;

/// The clock with GDA's calendar day (Pakistan time by default; Clock:TimeZone overrides it).
public sealed class OrganisationClock : IClock
{
  private readonly TimeZoneInfo _zone;

  public OrganisationClock(IConfiguration configuration)
  {
    var id = configuration["Clock:TimeZone"] ?? "Asia/Karachi";
    try { _zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
    catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
    {
      // no tz database in the image: Pakistan has no daylight saving, a fixed +05:00 is exact
      _zone = TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "Pakistan", "Pakistan");
    }
  }

  public DateTime UtcNow => DateTime.UtcNow;

  public DateOnly Today => DayOf(DateTime.UtcNow);

  public DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(LocalOf(utc));

  public DateTime LocalOf(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
}
