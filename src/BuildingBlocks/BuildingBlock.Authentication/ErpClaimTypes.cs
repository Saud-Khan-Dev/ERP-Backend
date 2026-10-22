/// Claim names shared by the token issuer (Identity) and every token consumer (business services).
/// Kept short deliberately: every claim is carried on every request.
public static class ErpClaimTypes
{
  /// User id (JWT "sub").
  public const string UserId = "sub";

  public const string Username = "username";
  /// The person's display name ("Ayesha Noor"), so every service writes the same name in its audit trail.
  public const string DisplayName = "name";
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

  /// Present ("change") on the token of an account that still holds a temporary password: it carries no permissions
  /// and no roles, and opens nothing but the caller's own /auth routes until the password is changed.
  public const string PasswordChange = "pwd";

  /// The account's access stamp when the token was issued. Identity raises the stamp whenever what the account may do
  /// changes (roles, exceptions, the role's access, where they work, switched off, password reset); every service asks
  /// Identity whether the token is still current, so an old token stops working within half a minute.
  public const string AccessStamp = "ast";

  /// One claim per town the person works on (Property and Taxation records). No claim: every town.
  public const string TownScope = "stn";

  /// One claim per office (HRM organization unit, its sub-units included) whose employees the person works on. No claim:
  /// every office.
  public const string OfficeScope = "sof";
}
