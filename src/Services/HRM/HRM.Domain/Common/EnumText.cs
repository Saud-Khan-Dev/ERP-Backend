using System.Text;

/// Enum names as words for messages: DeputationIn -> "deputation in", OnLeave -> "on leave".
public static class EnumText
{
  public static string Words(Enum value)
  {
    var name = value.ToString();
    var text = new StringBuilder(name.Length + 4);

    for (var i = 0; i < name.Length; i++)
    {
      if (i > 0 && char.IsUpper(name[i]))
        text.Append(' ');

      text.Append(char.ToLowerInvariant(name[i]));
    }

    return text.ToString();
  }

  /// As a label: Arrears -> "Arrears", FinalSettlement -> "Final settlement".
  public static string Label(Enum value)
  {
    var words = Words(value);
    return char.ToUpperInvariant(words[0]) + words[1..];
  }

  /// "an approved", "a draft" ("An approved" with capitalized).
  public static string WithArticle(Enum value, bool capitalized = false)
  {
    var words = Words(value);
    var article = "aeiou".Contains(words[0]) ? "an " : "a ";
    return (capitalized ? char.ToUpperInvariant(article[0]) + article[1..] : article) + words;
  }
}
