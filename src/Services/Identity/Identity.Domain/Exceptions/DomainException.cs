public class DomainException : Exception
{
  public DomainException(string message) : base(message) { }
}

/// Raised when authentication fails. Deliberately carries a single, generic message to the caller so
/// the API cannot leak whether a username exists — the specific reason is logged, not returned.
public sealed class InvalidCredentialsException : DomainException
{
  public const string GenericMessage = "Invalid username or password.";

  /// The real reason, for the audit log only.
  public string Reason { get; }

  public InvalidCredentialsException(string reason) : base(GenericMessage) => Reason = reason;
}

/// Raised when the account exists and the password is right, but the account may not sign in.
public sealed class AccountUnavailableException(string message) : DomainException(message);
