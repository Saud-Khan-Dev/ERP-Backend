/// Audit row for every sign-in attempt, successful or not.
///
/// UserId is nullable on purpose: someone may try to sign in as a username that does not exist, and
/// that attempt is exactly the one worth recording.
public class LoginAttempt : Entity<LoginAttemptId>
{
  public UserId? UserId { get; private set; }
  public string AttemptedUsername { get; private set; } = default!;
  public bool Succeeded { get; private set; }
  /// Why it failed — for administrators, never returned to the caller.
  public string? FailureReason { get; private set; }
  public IpAddress? IpAddress { get; private set; }
  public string? UserAgent { get; private set; }
  public DateTime AttemptedAt { get; private set; }

  public static LoginAttempt Success(UserId userId, string attemptedUsername, IpAddress? ip, string? userAgent, DateTime now) =>
      new()
      {
        Id = LoginAttemptId.Of(Guid.NewGuid()),
        UserId = userId,
        AttemptedUsername = Normalize(attemptedUsername),
        Succeeded = true,
        IpAddress = ip,
        UserAgent = userAgent,
        AttemptedAt = now
      };

  public static LoginAttempt Failure(UserId? userId, string attemptedUsername, string reason, IpAddress? ip, string? userAgent, DateTime now) =>
      new()
      {
        Id = LoginAttemptId.Of(Guid.NewGuid()),
        UserId = userId,
        AttemptedUsername = Normalize(attemptedUsername),
        Succeeded = false,
        FailureReason = reason,
        IpAddress = ip,
        UserAgent = userAgent,
        AttemptedAt = now
      };

  private static string Normalize(string username)
  {
    username = (username ?? string.Empty).Trim().ToLowerInvariant();
    return username.Length > Username.MaxLength ? username[..Username.MaxLength] : username;
  }
}
