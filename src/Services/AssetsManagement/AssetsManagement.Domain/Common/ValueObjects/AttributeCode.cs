using System.Text.RegularExpressions;

/// The JSONB key of a dynamic attribute inside asset.extra_attributes: manufacturer, ram_gb, operating_system ...
/// Immutable once any asset holds a value for it (enforced by the application layer).
public sealed record AttributeCode
{
  public const int MaxLength = 100;
  public string Value { get; }

  private static readonly Regex Pattern = new(@"^[a-z][a-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private AttributeCode(string value) => Value = value;

  public static AttributeCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Attribute code is required.");

    value = value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    if (value.Length > MaxLength)
      throw new DomainException($"Attribute code cannot exceed {MaxLength} characters.");

    if (!Pattern.IsMatch(value))
      throw new DomainException("Attribute code must start with a letter and contain only lower-case letters, digits and underscores (e.g. ram_gb).");

    return new AttributeCode(value);
  }
}
