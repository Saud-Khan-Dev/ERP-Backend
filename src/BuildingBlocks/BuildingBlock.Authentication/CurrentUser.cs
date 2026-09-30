using System.Security.Claims;
using Microsoft.AspNetCore.Http;

/// Reads the authenticated caller out of the current HTTP request's claims principal.
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
  private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

  public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

  public Guid? UserId => ReadGuid(ErpClaimTypes.UserId) ?? ReadGuid(ClaimTypes.NameIdentifier);

  public string? Username => Read(ErpClaimTypes.Username);

  public string? Email => Read(ErpClaimTypes.Email);

  public Guid? EmployeeId => ReadGuid(ErpClaimTypes.EmployeeId);

  public Guid? SessionId => ReadGuid(ErpClaimTypes.SessionId);

  public bool IsAuthorizedOfficer => string.Equals(Read(ErpClaimTypes.AuthorizedOfficer), "true", StringComparison.OrdinalIgnoreCase);

  public IReadOnlyCollection<string> Permissions =>
      Principal?.FindAll(ErpClaimTypes.Permission).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

  public IReadOnlyCollection<string> Roles =>
      Principal?.FindAll(ErpClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

  public bool HasPermission(string permissionCode) =>
      Principal?.HasClaim(ErpClaimTypes.Permission, permissionCode) ?? false;

  public string AuditName => Username ?? UserId?.ToString() ?? "system";

  private string? Read(string claimType) => Principal?.FindFirst(claimType)?.Value;

  private Guid? ReadGuid(string claimType) =>
      Guid.TryParse(Read(claimType), out var value) ? value : null;
}
