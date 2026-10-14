/// Notes the system adds to a record's remarks (e.g. "Reinstated by appeal APL-00001."): appended on a
/// new line, never replacing what the clerk wrote, and kept within the column's length.
public static class RemarkText
{
  public static string Append(string? existing, string note, int maxLength = 4000)
  {
    var text = string.IsNullOrWhiteSpace(existing) ? note : $"{existing.TrimEnd()}\n{note}";
    return text.Length <= maxLength ? text : text[^maxLength..];
  }
}
