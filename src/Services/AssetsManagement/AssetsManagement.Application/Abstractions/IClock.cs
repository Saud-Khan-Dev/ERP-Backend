/// The time, and the organisation's calendar day. GDA works in Pakistan time: "today" for a purchase date, a
/// disposal date or "the month has ended" is Pakistan's date, not the server's UTC one (before 05:00 they differ).
public interface IClock
{
  DateTime UtcNow { get; }

  /// Today's date where GDA works.
  DateOnly Today { get; }

  /// The organisation's calendar day of an instant.
  DateOnly DayOf(DateTime utc);
}
