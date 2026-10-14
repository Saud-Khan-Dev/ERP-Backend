/// Every change an administrator can make in the Administration module, recorded for accountability.
public enum ActivityAction
{
  // ---- accounts ----
  UserCreated,
  UserProfileUpdated,
  UserActivated,
  UserDeactivated,
  UserDeleted,
  UserUnlocked,
  PasswordReset,
  SessionsRevoked,
  AuthorizedOfficerSet,
  AuthorizedOfficerCleared,

  // ---- access ----
  RoleAssigned,
  RoleRemoved,
  PermissionOverrideSet,
  PermissionOverrideRemoved,

  // ---- roles ----
  RoleCreated,
  RoleUpdated,
  RoleDeleted,
  RolePermissionsAdded,
  RolePermissionRemoved,
  /// The role editor's save: every permission added and removed, in one line.
  RolePermissionsChanged,

  // ---- configuration ----
  EmployeeCodeTemplateUpdated,
  SecuritySettingsUpdated
}
