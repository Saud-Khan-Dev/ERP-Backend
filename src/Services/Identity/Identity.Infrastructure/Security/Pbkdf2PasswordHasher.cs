using Microsoft.AspNetCore.Identity;

/// Wraps ASP.NET Core's PasswordHasher — PBKDF2-HMAC-SHA512, 210k iterations, per-password salt,
/// fixed-time comparison, with a versioned format so the work factor can be raised later.
///
/// A plaintext password never reaches any other layer: Application sees only this interface.
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
  // the type argument is only a marker; the hasher itself is generic over "some user"
  private readonly PasswordHasher<object> _hasher = new();
  private static readonly object Marker = new();

  public string Hash(string password)
  {
    if (string.IsNullOrEmpty(password))
      throw new DomainException("Password is required.");

    return _hasher.HashPassword(Marker, password);
  }

  public PasswordVerificationOutcome Verify(string hash, string providedPassword)
  {
    if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(providedPassword))
      return PasswordVerificationOutcome.Failed;

    // a malformed stored hash must fail closed, not throw
    try
    {
      return _hasher.VerifyHashedPassword(Marker, hash, providedPassword) switch
      {
        PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
        _ => PasswordVerificationOutcome.Failed
      };
    }
    catch (FormatException)
    {
      return PasswordVerificationOutcome.Failed;
    }
  }
}
