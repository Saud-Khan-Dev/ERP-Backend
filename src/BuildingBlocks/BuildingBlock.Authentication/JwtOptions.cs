/// Token settings shared by the issuer (Identity) and every validator (business services).
/// Bound from the "Jwt" configuration section; the signing key must come from configuration
/// or an environment variable, never from source.
public sealed class JwtOptions
{
  public const string SectionName = "Jwt";

  public string Issuer { get; set; } = "erp-identity";

  public string Audience { get; set; } = "erp";

  /// Symmetric signing key. Must be at least 32 bytes for HS256.
  public string SigningKey { get; set; } = string.Empty;

  public int AccessTokenMinutes { get; set; } = 15;

  public int RefreshTokenDays { get; set; } = 7;
}
