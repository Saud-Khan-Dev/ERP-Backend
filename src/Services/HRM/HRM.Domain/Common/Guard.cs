/// Small shared checks so every aggregate words the same rule the same way. Messages are shown to end users.
public static class Guard
{
  /// Optional free text: trimmed, null when blank, length-checked.
  public static string? Text(string? value, int maxLength, string field)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    value = value.Trim();

    if (value.Length > maxLength)
      throw new DomainException($"{field} cannot exceed {maxLength} characters.");

    return value;
  }

  public static string RequiredText(string? value, int maxLength, string field) =>
      Text(value, maxLength, field) ?? throw new DomainException($"{field} is required.");

  /// A code typed by a person (org unit, post, salary component ...): trimmed, upper case, letters, digits, '-', '_', '/'.
  public static string Code(string? value, int maxLength, string field)
  {
    var code = RequiredText(value, maxLength, field).ToUpperInvariant();

    foreach (var c in code)
    {
      if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '/'))
        throw new DomainException($"{field} may only contain letters, digits, '-', '_' and '/' (e.g. GDA-ENG-01).");
    }

    return code;
  }

  public static string? OptionalCode(string? value, int maxLength, string field) =>
      string.IsNullOrWhiteSpace(value) ? null : Code(value, maxLength, field);

  public static decimal NotNegative(decimal value, string field) =>
      value < 0 ? throw new DomainException($"{field} cannot be negative.") : value;

  public static decimal? NotNegative(decimal? value, string field) =>
      value is < 0 ? throw new DomainException($"{field} cannot be negative.") : value;

  public static int NotNegative(int value, string field) =>
      value < 0 ? throw new DomainException($"{field} cannot be negative.") : value;

  public static decimal Positive(decimal value, string field) =>
      value <= 0 ? throw new DomainException($"{field} must be greater than zero.") : value;

  public static int Positive(int value, string field) =>
      value <= 0 ? throw new DomainException($"{field} must be greater than zero.") : value;

  public static decimal Between(decimal value, decimal min, decimal max, string field) =>
      value < min || value > max ? throw new DomainException($"{field} must be between {min} and {max}.") : value;

  public static decimal? Between(decimal? value, decimal min, decimal max, string field) =>
      value.HasValue ? Between(value.Value, min, max, field) : null;

  public static int Between(int value, int min, int max, string field) =>
      value < min || value > max ? throw new DomainException($"{field} must be between {min} and {max}.") : value;

  public static int? Between(int? value, int min, int max, string field) =>
      value.HasValue ? Between(value.Value, min, max, field) : null;

  /// Money columns are numeric(14,2).
  public static decimal Money(decimal value, string field) => decimal.Round(NotNegative(value, field), 2, MidpointRounding.AwayFromZero);

  public static decimal? Money(decimal? value, string field) => value.HasValue ? Money(value.Value, field) : null;

  public static void DateOrder(DateOnly? from, DateOnly? to, string fromField, string toField)
  {
    if (from.HasValue && to.HasValue && to.Value < from.Value)
      throw new DomainException($"{toField} cannot be before {fromField}.");
  }

  public static Guid Actor(Guid? userId, string action) =>
      userId is { } id && id != Guid.Empty ? id : throw new DomainException($"Only a signed-in user can {action}.");
}
