using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// Whether an access token still speaks for its account: its sign-in session is open, the account is on and its access
/// stamp is the account's current one. A token signed hours ago stays valid as a signature; this is what makes switching
/// an account off, signing someone out or changing their access take effect at once instead of when the token expires.
public interface ITokenStateChecker
{
  Task<TokenVerdict> CheckAsync(ClaimsPrincipal user, string rawToken, CancellationToken cancellationToken);
}

/// Current: the token may be used. Reason: why not, in words ("Signed out", "Access changed") - the 401 carries it.
public sealed record TokenVerdict(bool Current, string? Reason)
{
  public static readonly TokenVerdict Ok = new(true, null);
  public static TokenVerdict Stale(string reason) => new(false, reason);
}

/// `GET /auth/token-state` on Identity (with the token itself as the bearer).
public sealed record TokenStateResponse(bool Current, string? Reason);

/// Where the other services find Identity (`Services:IdentityBaseUrl`, compose sets it). Every business service asks it
/// whether a token is current, and reads the separation-of-duty rules from it.
public sealed class ErpServiceUrls
{
  public const string SectionName = "Services";

  public string IdentityBaseUrl { get; set; } = "http://identity-api:8080";
}

/// The business services' checker: asks Identity, and remembers the answer for half a minute per session and stamp, so a
/// busy screen costs one call every 30 seconds, not one per request. When Identity does not answer, the token is let
/// through (it still expires on its own) rather than stopping every service with it - and the log says so.
public sealed class HttpTokenStateChecker(IHttpClientFactory httpClients, IMemoryCache cache, ILogger<HttpTokenStateChecker> logger) : ITokenStateChecker
{
  public const string ClientName = "erp-identity";

  private static readonly TimeSpan Remember = TimeSpan.FromSeconds(30);

  public async Task<TokenVerdict> CheckAsync(ClaimsPrincipal user, string rawToken, CancellationToken cancellationToken)
  {
    var session = user.FindFirst(ErpClaimTypes.SessionId)?.Value;
    var stamp = user.FindFirst(ErpClaimTypes.AccessStamp)?.Value;
    if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(stamp))
      return TokenVerdict.Stale("Signed in before this version - sign in again");

    var key = $"token-state:{session}:{stamp}";
    if (cache.TryGetValue(key, out TokenVerdict? known) && known is not null)
      return known;

    try
    {
      using var request = new HttpRequestMessage(HttpMethod.Get, "auth/token-state");
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
      using var response = await httpClients.CreateClient(ClientName).SendAsync(request, cancellationToken);
      TokenVerdict verdict;
      if (response.IsSuccessStatusCode)
      {
        var state = await response.Content.ReadFromJsonAsync<TokenStateResponse>(cancellationToken);
        verdict = state is { Current: true } ? TokenVerdict.Ok : TokenVerdict.Stale(state?.Reason ?? "Signed out");
      }
      else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
      {
        var why = response.Headers.TryGetValues("X-Token-Stale", out var values) ? Uri.UnescapeDataString(values.First()) : null;
        verdict = TokenVerdict.Stale(why ?? "Signed out");
      }
      else
      {
        logger.LogWarning("Identity answered {Status} to a token check; letting the token through.", (int)response.StatusCode);
        return TokenVerdict.Ok;
      }
      cache.Set(key, verdict, Remember);
      return verdict;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      logger.LogWarning("Identity did not answer a token check ({Message}); letting the token through.", ex.Message);
      return TokenVerdict.Ok;
    }
  }
}
