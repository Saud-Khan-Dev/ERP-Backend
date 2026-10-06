using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// Enums travel as their names ("ScheduledSeat"), as everywhere in the ERP. Reading is lenient: case is ignored and the
/// schema's snake_case labels ("scheduled_seat") are accepted as well. Numbers and unknown names are refused.
public sealed class LenientEnumConverterFactory : JsonConverterFactory
{
  public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

  public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
      (JsonConverter)Activator.CreateInstance(typeof(LenientEnumConverter<>).MakeGenericType(typeToConvert))!;
}

public sealed partial class LenientEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
  private static readonly string Allowed = string.Join(", ", Enum.GetNames<T>().Select(n => Words().Replace(n, "_$1").ToLowerInvariant()));

  public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    var raw = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
    var text = raw?.Trim().Replace("_", string.Empty).Replace("-", string.Empty);
    if (!string.IsNullOrEmpty(text) && char.IsLetter(text[0]) && Enum.TryParse<T>(text, ignoreCase: true, out var value) && Enum.IsDefined(value))
      return value;

    throw new JsonException(raw is null
      ? $"'{reader.TokenType.ToString().ToLowerInvariant()}' given; a name is expected. Use one of: {Allowed}."
      : $"'{raw}' is not valid here. Use one of: {Allowed}.");
  }

  public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());

  [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
  private static partial Regex Words();
}
