using System.Text.RegularExpressions;

/// A permission code is always MODULE.ACTION — e.g. ASSETS.CREATE. Free text is not allowed:
/// the module and the action must each be valid on their own.
public sealed record PermissionCode
{
  public const int MaxLength = 100;

  public string Value { get; }
  public string Module { get; }
  public string Action { get; }

  private static readonly Regex Pattern = new(@"^([A-Z0-9][A-Z0-9_]*)\.([A-Z0-9][A-Z0-9_]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

  private PermissionCode(string value, string module, string action)
  {
    Value = value;
    Module = module;
    Action = action;
  }

  public static PermissionCode Of(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      throw new DomainException("Permission code is required.");

    value = value.Trim().ToUpperInvariant();

    if (value.Length > MaxLength)
      throw new DomainException($"Permission code cannot exceed {MaxLength} characters.");

    var match = Pattern.Match(value);
    if (!match.Success)
      throw new DomainException("Permission code must be in the form MODULE.ACTION (e.g. ASSETS.CREATE).");

    return new PermissionCode(value, match.Groups[1].Value, match.Groups[2].Value);
  }

  public static PermissionCode Of(LookupCode module, PermissionAction action) =>
      Of($"{module.Value}.{action.ToString().ToUpperInvariant()}");

  public override string ToString() => Value;
}
