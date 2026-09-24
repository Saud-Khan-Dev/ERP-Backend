using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// Issues the short-lived access token (a signed JWT carrying the caller's permissions) and the
/// long-lived refresh token (an opaque random string, of which only a hash is ever stored).
///
/// Permissions travel inside the token so business services can authorize without calling back
/// here on every request. The token is deliberately short-lived so a revoked role takes effect
/// within minutes rather than days.
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
  private readonly JwtOptions _options = options.Value;

  public AccessToken CreateAccessToken(
      User user,
      SessionId sessionId,
      IReadOnlyCollection<string> permissionCodes,
      IReadOnlyCollection<string> roleCodes)
  {
    ArgumentNullException.ThrowIfNull(user);
    ArgumentNullException.ThrowIfNull(sessionId);

    var now = DateTime.UtcNow;
    var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
      new(ErpClaimTypes.Username, user.Username.Value),
      new(ErpClaimTypes.Email, user.Email.Value),
      new(ErpClaimTypes.SessionId, sessionId.Value.ToString())
    };

    if (user.EmployeeId is { } employeeId)
      claims.Add(new Claim(ErpClaimTypes.EmployeeId, employeeId.ToString()));

    claims.AddRange(roleCodes.Select(r => new Claim(ErpClaimTypes.Role, r)));
    claims.AddRange(permissionCodes.Select(p => new Claim(ErpClaimTypes.Permission, p)));

    var credentials = new SigningCredentials(
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
      SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
      issuer: _options.Issuer,
      audience: _options.Audience,
      claims: claims,
      notBefore: now,
      expires: expiresAt,
      signingCredentials: credentials);

    return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
  }

  public RefreshToken CreateRefreshToken()
  {
    // opaque, not a JWT: it carries no claims and is only ever matched against a stored hash
    var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    return new RefreshToken(value, HashRefreshToken(value), DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
  }

  /// SHA-256 is right here, unlike for passwords: the token is already 256 bits of entropy, so
  /// there is nothing to brute force and the lookup needs to be fast.
  public string HashRefreshToken(string refreshToken)
  {
    if (string.IsNullOrWhiteSpace(refreshToken))
      throw new DomainException("A refresh token is required.");

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
  }
}
