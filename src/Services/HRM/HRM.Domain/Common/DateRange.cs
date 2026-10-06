/// An inclusive date range; To = null means open-ended. Matches the schema's daterange(from, to, '[]') overlap rules.
public readonly record struct DateRange(DateOnly From, DateOnly? To)
{
  public bool Contains(DateOnly date) => date >= From && (To is null || date <= To.Value);

  public bool Overlaps(DateRange other) =>
      (To is null || other.From <= To.Value) && (other.To is null || From <= other.To.Value);

  public bool Overlaps(DateOnly from, DateOnly? to) => Overlaps(new DateRange(from, to));

  /// The part of this range inside [from, to], or null when they do not meet.
  public DateRange? Clip(DateOnly from, DateOnly to)
  {
    var start = From > from ? From : from;
    var end = To is null || To.Value > to ? to : To.Value;
    return end < start ? null : new DateRange(start, end);
  }

  public int Days => To is null ? throw new InvalidOperationException("An open-ended range has no length.") : To.Value.DayNumber - From.DayNumber + 1;

  public static void EnsureValid(DateOnly from, DateOnly? to, string fromField = "Effective from", string toField = "Effective to") =>
      Guard.DateOrder(from, to, fromField, toField);
}
