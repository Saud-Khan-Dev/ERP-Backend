/// Claim names shared by the token issuer (Identity) and every token consumer (business services).
/// Kept short deliberately: every claim is carried on every request.
public static class ErpClaimTypes
{
  /// User id (JWT "sub").
  public const string UserId = "sub";

  public const string Username = "username";
  public const string Email = "email";

  /// Optional HRM employee reference; absent when the account is not linked to an employee.
  public const string EmployeeId = "emp";

  /// One claim per effective permission code, e.g. "ASSETS.CREATE".
  public const string Permission = "perm";

  /// One claim per role code, for display and coarse checks only — never for authorization decisions.
  public const string Role = "role";

  /// Session id, so a token can be tied back to the session that issued it.
  public const string SessionId = "sid";

  /// Present ("true") only for an officer authorized by the DG (GDA Act s.2(a-i), s.28, s.30).
  public const string AuthorizedOfficer = "ao";
}
