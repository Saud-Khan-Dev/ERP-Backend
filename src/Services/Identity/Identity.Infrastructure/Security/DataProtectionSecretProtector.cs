using Microsoft.AspNetCore.DataProtection;

/// Encrypts MFA seeds at rest using ASP.NET Data Protection.
///
/// Note for deployment: the default key ring is per-machine, so a multi-instance deployment must
/// point Data Protection at shared, persisted keys or protected secrets become unreadable on the
/// other instances.
public sealed class DataProtectionSecretProtector : ISecretProtector
{
  private const string Purpose = "Identity.MfaSecret.v1";

  private readonly IDataProtector _protector;

  public DataProtectionSecretProtector(IDataProtectionProvider provider)
  {
    ArgumentNullException.ThrowIfNull(provider);
    _protector = provider.CreateProtector(Purpose);
  }

  public string Protect(string plaintext)
  {
    if (string.IsNullOrEmpty(plaintext))
      throw new DomainException("A secret is required.");

    return _protector.Protect(plaintext);
  }

  public string Unprotect(string protectedValue)
  {
    if (string.IsNullOrEmpty(protectedValue))
      throw new DomainException("A protected secret is required.");

    return _protector.Unprotect(protectedValue);
  }
}
