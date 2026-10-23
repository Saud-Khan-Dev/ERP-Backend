using System.Security.Claims;
using Microsoft.AspNetCore.Http;

/// Reads the authenticated caller out of the current HTTP request's claims principal.
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
  private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

  public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

  public Guid? UserId => ReadGuid(ErpClaimTypes.UserId) ?? ReadGuid(ClaimTypes.NameIdentifier);

  public string? Username => Read(ErpClaimTypes.Username);

  public string? DisplayName => Read(ErpClaimTypes.DisplayName);

  public string? Email => Read(ErpClaimTypes.Email);

  public Guid? EmployeeId => ReadGuid(ErpClaimTypes.EmployeeId);

  public Guid? SessionId => ReadGuid(ErpClaimTypes.SessionId);

  public long? AccessStamp => long.TryParse(Read(ErpClaimTypes.AccessStamp), out var stamp) ? stamp : null;

  public IReadOnlySet<Guid>? TownScope => ReadScope(ErpClaimTypes.TownScope);

  public IReadOnlySet<Guid>? OfficeScope => ReadScope(ErpClaimTypes.OfficeScope);

  public bool IsSuperAdmin => Principal?.HasClaim(ErpClaimTypes.Role, SuperAdminRoleCode) ?? false;

  /// The built-in role's code (Identity's Role.SuperAdminCode).
  public const string SuperAdminRoleCode = "SUPER_ADMIN";

  public bool IsAuthorizedOfficer => string.Equals(Read(ErpClaimTypes.AuthorizedOfficer), "true", StringComparison.OrdinalIgnoreCase);

  public IReadOnlyCollection<string> Permissions =>
      Principal?.FindAll(ErpClaimTypes.Permission).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

  public IReadOnlyCollection<string> Roles =>
      Principal?.FindAll(ErpClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

  public bool HasPermission(string permissionCode) =>
      Principal?.HasClaim(ErpClaimTypes.Permission, permissionCode) ?? false;

  /// The name an audit trail writes: the display name, else the username, else the id - never empty.
  public string AuditName => DisplayName ?? Username ?? UserId?.ToString() ?? "system";

  private string? Read(string claimType) => Principal?.FindFirst(claimType)?.Value;

  /// No claim of the kind: no restriction (null). Otherwise the ids the claims carry.
  private IReadOnlySet<Guid>? ReadScope(string claimType)
  {
    var claims = Principal?.FindAll(claimType).ToList();
    if (claims is null || claims.Count == 0)
      return null;
    return claims.Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToHashSet();
  }

  private Guid? ReadGuid(string claimType) =>
      Guid.TryParse(Read(claimType), out var value) ? value : null;
}
