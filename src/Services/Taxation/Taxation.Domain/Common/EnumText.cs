using System.Text;

/// Enum names in the forms the module needs: UPPER_SNAKE for the database (QrCode -> QR_CODE, as the DBML lists the
/// values) and words for messages (ShowCause -> "show cause").
public static class EnumText
{
  public static string ToUpperSnake(string name)
  {
    var text = new StringBuilder(name.Length + 4);
    for (var i = 0; i < name.Length; i++)
    {
      if (i > 0 && char.IsUpper(name[i]))
        text.Append('_');
      text.Append(char.ToUpperInvariant(name[i]));
    }
    return text.ToString();
  }

  public static string UpperSnake<TEnum>(TEnum value) where TEnum : struct, Enum => ToUpperSnake(value.ToString());

  public static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum =>
      Enum.TryParse<TEnum>(text.Replace("_", string.Empty), ignoreCase: true, out var value) && Enum.IsDefined(value)
        ? value
        : throw new InvalidOperationException($"'{text}' is not a {typeof(TEnum).Name} value.");

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
}
