using System.Net;

/// Caller IP recorded on sessions and login attempts. 45 characters covers IPv6.
public sealed record IpAddress
{
  public const int MaxLength = 45;

  public string Value { get; }

  private IpAddress(string value) => Value = value;

  public static IpAddress? OfNullable(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return null;

    value = value.Trim();

    if (value.Length > MaxLength || !IPAddress.TryParse(value, out _))
      throw new DomainException("IP address is not valid.");

    return new IpAddress(value);
  }

  public override string ToString() => Value;
}
