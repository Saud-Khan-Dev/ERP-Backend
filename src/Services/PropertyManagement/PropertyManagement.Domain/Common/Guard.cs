/// Small shared checks so every aggregate words the same rule the same way.
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

  public static decimal NotNegative(decimal value, string field) =>
      value < 0 ? throw new DomainException($"{field} cannot be negative.") : value;

  public static decimal? NotNegative(decimal? value, string field) =>
      value is < 0 ? throw new DomainException($"{field} cannot be negative.") : value;

  public static decimal Positive(decimal value, string field) =>
      value <= 0 ? throw new DomainException($"{field} must be greater than zero.") : value;

  /// Shares and percentages: decimal(7,4), 0 < value <= 100.
  public static decimal SharePct(decimal value, string field)
  {
    if (value <= 0 || value > 100)
      throw new DomainException($"{field} must be greater than 0 and at most 100.");

    return decimal.Round(value, 4);
  }

  public static void DateOrder(DateOnly? from, DateOnly? to, string fromField, string toField)
  {
    if (from.HasValue && to.HasValue && to.Value < from.Value)
      throw new DomainException($"{toField} cannot be before {fromField}.");
  }
}
