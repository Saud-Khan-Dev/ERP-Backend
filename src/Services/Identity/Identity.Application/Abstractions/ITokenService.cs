public sealed record AccessToken(string Value, DateTime ExpiresAt);

public sealed record RefreshToken(string Value, string Hash, DateTime ExpiresAt);

/// Issues access tokens and refresh tokens. Implemented in Infrastructure.
public interface ITokenService
{
  AccessToken CreateAccessToken(
      User user,
      SessionId sessionId,
      IReadOnlyCollection<string> permissionCodes,
      IReadOnlyCollection<string> roleCodes,
      TimeSpan lifetime);

  /// Generates a cryptographically random refresh token plus the hash to store.
  RefreshToken CreateRefreshToken(TimeSpan lifetime);

  /// Hashes a refresh token supplied by a client, so it can be matched against a stored session.
  string HashRefreshToken(string refreshToken);
}
