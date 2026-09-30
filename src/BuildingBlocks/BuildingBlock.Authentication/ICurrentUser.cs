/// The authenticated caller, resolved from the access token.
///
/// This is what replaces the hard-coded "Saud-DEV" in every service's AuditableEntityInterceptors,
/// and what supplies actor fields (performedBy / changedBy / approvedBy) so they can no longer be
/// spoofed through the request body.
public interface ICurrentUser
{
  bool IsAuthenticated { get; }

  /// Null when the request is anonymous.
  Guid? UserId { get; }

  string? Username { get; }

  string? Email { get; }

  /// HRM employee reference, when the account is linked to one.
  Guid? EmployeeId { get; }

  /// Effective permission codes carried by the token.
  IReadOnlyCollection<string> Permissions { get; }

  IReadOnlyCollection<string> Roles { get; }

  /// Session the access token was issued for.
  Guid? SessionId { get; }

  /// Officer authorized by the DG (GDA Act s.2(a-i)): may impose fines (s.28) and file complaints (s.30).
  bool IsAuthorizedOfficer { get; }

  bool HasPermission(string permissionCode);

  /// Identifier written to audit columns. Falls back to "system" for background work.
  string AuditName { get; }
}
