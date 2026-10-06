using System.Text.RegularExpressions;

/// The sentence inside a DomainException (its message carries the platform's "Domain Exception "..." throws from
/// Domain layer" wrapper), for lists of problems in batch results.
public static partial class DomainMessages
{
  public static string Text(DomainException error)
  {
    var match = Wrapper().Match(error.Message);
    return match.Success ? match.Groups[1].Value.Trim() : error.Message;
  }

  [GeneratedRegex("^Domain Exception \"(.*)\" throws from Domain layer$", RegexOptions.Singleline)]
  private static partial Regex Wrapper();
}
