public enum PasswordVerificationOutcome
{
  Failed,
  Success,
  /// Correct password, but hashed with outdated parameters — re-hash it on the way through.
  SuccessRehashNeeded
}

/// Implemented in Infrastructure. Application never sees the hashing algorithm.
public interface IPasswordHasher
{
  string Hash(string password);

  PasswordVerificationOutcome Verify(string hash, string providedPassword);
}
